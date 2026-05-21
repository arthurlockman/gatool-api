using Amazon.DynamoDBv2;
using Amazon.S3;
using Amazon.SecretsManager;
using Amazon.SimpleEmailV2;
using Fido2NetLib;
using GAToolAPI.Attributes;
using GAToolAPI.AuthExtensions;
using GAToolAPI.Helpers;
using GAToolAPI.Jobs;
using GAToolAPI.Middleware;
using GAToolAPI.Services;
using GAToolAPI.Services.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using NewRelic.LogEnrichers.Serilog;
using NSwag;
using NSwag.Generation.Processors.Security;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithNewRelicLogsInContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Fetch ECS task metadata (no-ops gracefully outside ECS)
    var ecsMetadata = await EcsMetadataService.FetchAsync();
    builder.Services.AddSingleton(ecsMetadata);
    if (ecsMetadata.IsRunningOnEcs)
        Log.Information("Running on ECS: Task {TaskId} in cluster {Cluster} ({AZ})",
            ecsMetadata.TaskId, ecsMetadata.ClusterName, ecsMetadata.AvailabilityZone);

    // Preload secrets from AWS Secrets Manager
    var smClient = new AmazonSecretsManagerClient();
    var secretNames = new[]
    {
        "FRCApiKey", "TBAApiKey", "FTCApiKey", "CasterstoolApiKey", "TOAApiKey",
        "FRCCurrentSeason", "FTCCurrentSeason",
        "MailChimpAPIKey", "MailchimpAPIURL", "MailchimpListID",
        "NewRelicLicenseKey",
        "MailchimpWebhookSecret"
    };
    var preloadedSecrets = await AwsSecretProvider.PreloadSecretsAsync(smClient, secretNames);
    var secretProvider = new AwsSecretProvider(smClient, preloadedSecrets);

    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.With(new EcsSerilogEnricher(ecsMetadata))
        .MinimumLevel.Override("Microsoft.AspNetCore.Hosting", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore.Mvc", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore.Routing", LogEventLevel.Warning)
        // CORS logs "CORS policy execution successful." at Information for every
        // preflight + cross-origin request. Drop to Warning so failures still surface.
        .MinimumLevel.Override("Microsoft.AspNetCore.Cors", LogEventLevel.Warning)
        // FusionCache emits a lot of Information/Debug per cache op (factory,
        // backplane, distributed cache). Warning is enough in production.
        .MinimumLevel.Override("ZiggyCreatures.Caching.Fusion", LogEventLevel.Warning));

    builder.Services.AddAuthentication(options =>
        {
            // Self-issued ES256 JWT is the only accepted scheme.
            options.DefaultAuthenticateScheme = "GatoolJwt";
            options.DefaultChallengeScheme = "GatoolJwt";
        })
        .AddJwtBearer("GatoolJwt", options =>
        {
            // Self-issued JWT: ECDSA P-256 signing key from Secrets Manager.
            // Configured asynchronously below once the DI container is built.
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = async ctx =>
                {
                    if (ctx.Options.TokenValidationParameters.IssuerSigningKey == null)
                    {
                        var tokenSvc = ctx.HttpContext.RequestServices
                            .GetRequiredService<TokenService>();
                        var key = await tokenSvc.GetValidationKeyAsync(ctx.HttpContext.RequestAborted);
                        ctx.Options.TokenValidationParameters = tokenSvc.BuildValidationParameters(key);
                    }
                }
            };
        });

    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("user", policy =>
        {
            policy.AuthenticationSchemes = ["GatoolJwt"];
            policy.Requirements.Add(new HasRoleRequirement("user"));
        })
        .AddPolicy("admin", policy =>
        {
            policy.AuthenticationSchemes = ["GatoolJwt"];
            policy.Requirements.Add(new HasRoleRequirement("admin"));
        });
    builder.Services.AddSingleton<IAuthorizationHandler, HasRoleHandler>();

    builder.Services.AddSingleton<ISecretProvider>(secretProvider);
    builder.Services.AddSingleton<IAmazonSecretsManager>(smClient);
    builder.Services.AddAWSService<IAmazonS3>();
    builder.Services.AddAWSService<IAmazonDynamoDB>();
    builder.Services.AddAWSService<IAmazonSimpleEmailServiceV2>();

    // Custom auth services (email OTP + WebAuthn passkeys)
    builder.Services.AddSingleton<AuthSigningKeyProvider>();
    builder.Services.AddSingleton<OtpPepperProvider>();
    builder.Services.AddSingleton<AuthRepository>();
    builder.Services.AddSingleton<AuthEmailService>();
    builder.Services.AddSingleton<RedisRateLimiter>();
    builder.Services.AddSingleton<TokenService>();
    builder.Services.AddSingleton<OtpService>();
    builder.Services.AddScoped<PasskeyService>();
    builder.Services.AddMemoryCache();
    builder.Services.AddHttpClient();
    builder.Services.AddSingleton<CommunityAaguidService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<CommunityAaguidService>());

    // Fido2 / WebAuthn server. ServerDomain is the WebAuthn rpId — must be an apex
    // domain or registrable suffix shared by all origins. gatool.org covers
    // gatool.org + beta.gatool.org. Localhost dev gets its own override via env.
    //
    // We also wire up the FIDO Metadata Service so we can resolve AAGUIDs to
    // human-readable authenticator names (e.g. "iCloud Keychain", "1Password",
    // "YubiKey 5") on passkey registration. Metadata is fetched from
    // mds3.fidoalliance.org and cached in Redis (via IDistributedCache below).
    builder.Services.AddFido2(o =>
        {
            o.ServerDomain = builder.Configuration["WebAuthn:ServerDomain"] ?? "gatool.org";
            o.ServerName = "gatool";
            o.Origins = (builder.Configuration["WebAuthn:Origins"]
                            ?? "https://gatool.org,https://beta.gatool.org,http://localhost:3000")
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToHashSet();
            o.TimestampDriftTolerance = 300_000;
        })
        .AddCachedMetadataService(b => b.AddFidoMetadataRepository());

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(b => b
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());

        // Auth endpoints (login, OTP, passkey, refresh) are restricted to the
        // first-party UI origins. Other origins can still hit the public read
        // endpoints (covered by the default policy above) but cannot initiate
        // a login flow against this API.
        options.AddPolicy("AuthOrigins", b => b
            .WithOrigins(
                "https://gatool.org",
                "https://beta.gatool.org",
                "http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader());
    });

    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<BulkRequestEnrichmentFilter>();
    });

    // Add HttpContextAccessor for RedisCache.IgnoreCurrentRequest() functionality
    builder.Services.AddHttpContextAccessor();

    builder.Services.AddOpenApiDocument(config =>
    {
        config.Title = "GATool API";
        config.Version = "v3";

        config.AddSecurity("JWT", [], new OpenApiSecurityScheme
        {
            Type = OpenApiSecuritySchemeType.ApiKey,
            Name = "Authorization",
            In = OpenApiSecurityApiKeyLocation.Header,
            Description = "Type into the textbox: Bearer {your JWT token}."
        });

        config.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("JWT"));
    });

    var redisConfig = new ConfigurationOptions
    {
        EndPoints =
        {
            {
                // ReSharper disable once NotResolvedInText
                builder.Configuration["Redis:Host"] ?? throw new ArgumentNullException("Redis:Host"),
                // ReSharper disable once NotResolvedInText
                int.Parse(builder.Configuration["Redis:Port"] ?? throw new ArgumentNullException("Redis:Port"))
            }
        },
        Password = builder.Configuration["Redis:Password"] ?? null,
        Ssl = builder.Configuration.GetValue<bool?>("Redis:UseTls") ?? false,
        AllowAdmin = true,
        // Don't crash on first connect failure: return a multiplexer that keeps
        // retrying in the background. FusionCache will operate L1-only until
        // Redis is reachable, then transparently re-engage L2 + backplane.
        AbortOnConnectFail = false,
        ConnectRetry = 5,
        ConnectTimeout = 5000,
        ReconnectRetryPolicy = new ExponentialRetry(500)
    };
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfig));

    // FusionCache: shared L1 (memory) + L2 (Redis via shared multiplexer) + Redis backplane.
    // L1 + single-flight factory provides intra-task stampede protection.
    // L2 + backplane coordinate cache state across all ECS tasks so a popular key
    // hits downstream APIs (FRC/TBA/Statbotics/etc.) at most once per fleet.
    builder.Services.AddSingleton<IDistributedCache>(sp =>
        new Microsoft.Extensions.Caching.StackExchangeRedis.RedisCache(new RedisCacheOptions
        {
            ConnectionMultiplexerFactory = () =>
                Task.FromResult(sp.GetRequiredService<IConnectionMultiplexer>())
        }));

    builder.Services.AddFusionCache()
        .WithDefaultEntryOptions(new FusionCacheEntryOptions
        {
            Duration = TimeSpan.FromMinutes(1),

            // Fail-safe: when downstream errors and we have a stale value, return it.
            // Critical for an announcer tool — better stale data than a broken broadcast.
            IsFailSafeEnabled = true,
            FailSafeMaxDuration = TimeSpan.FromHours(24),
            FailSafeThrottleDuration = TimeSpan.FromSeconds(30),

            // Soft timeout: if a factory takes longer than this AND we have stale data,
            // return the stale data and let the factory finish in the background.
            FactorySoftTimeout = TimeSpan.FromMilliseconds(500),
            // Hard timeout: absolute ceiling for a factory call (no stale data path).
            FactoryHardTimeout = TimeSpan.FromSeconds(10),

            // Eager refresh: pro-actively refresh entries at 80% of duration to keep
            // hot keys fresh without ever exposing a "real" miss to a request.
            EagerRefreshThreshold = 0.8f,

            // Allow background updates triggered by timeouts/eager-refresh to populate L2.
            AllowBackgroundDistributedCacheOperations = true,
            AllowBackgroundBackplaneOperations = true
        })
        .WithSerializer(new FusionCacheSystemTextJsonSerializer(new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        }))
        .WithRegisteredDistributedCache()
        .WithBackplane(sp => new RedisBackplane(new RedisBackplaneOptions
        {
            ConnectionMultiplexerFactory = () =>
                Task.FromResult(sp.GetRequiredService<IConnectionMultiplexer>())
        }));

    builder.Services.AddHttpClient();

    // Per-request holder for the desired downstream cache TTL. Populated by
    // RedisCacheAttribute when an action runs; left null on background-job code paths
    // (no controller -> no caching).
    builder.Services.AddScoped<CacheTtlContext>();

    builder.Services.AddScoped<FRCApiService>();
    builder.Services.AddScoped<FirstGlobalApiService>();
    builder.Services.AddScoped<TBAApiService>();
    builder.Services.AddScoped<StatboticsApiService>();
    builder.Services.AddScoped<CasterstoolApiService>();
    builder.Services.AddScoped<FTCApiService>();
    builder.Services.AddScoped<FTCScoutApiService>();
    builder.Services.AddScoped<TOAApiService>();
    builder.Services.AddSingleton<UserStorageService>();
    builder.Services.AddSingleton<HighScoreRepository>();
    builder.Services.AddScoped<TeamDataService>();
    builder.Services.AddScoped<ScheduleService>();
    builder.Services.AddScoped<FTCScheduleService>();
    builder.Services.AddSingleton<MailchimpWebhookService>();

    // Register job services
    builder.Services.AddScoped<JobRunnerService>();
    builder.Services.AddScoped<UpdateGlobalHighScoresJob>();

    var app = builder.Build();

    // Initialize the ServiceLocator for RedisCache functionality
    ServiceLocator.ServiceProvider = app.Services;

    // Check if we're running a job instead of the web API
    if (args.Length > 0 && args[0] == "--job")
    {
        var scope = app.Services.CreateScope();
        var jobRunner = scope.ServiceProvider.GetRequiredService<JobRunnerService>();
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: --job <job-name>");
            Console.WriteLine("Available jobs:");

            foreach (var j in jobRunner.GetAvailableJobs()) Console.WriteLine($"  - {j}");
            return;
        }

        var jobName = args[1];
        await jobRunner.RunJobAsync(jobName);
        return;
    }

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<UndefinedRouteParameterMiddleware>();
    app.UseSerilogRequestLogging(options =>
    {
        // Demote noisy, expected requests to Verbose so they're filtered out of
        // production sinks but still available locally if MinimumLevel is lowered.
        // - /livecheck: ALB target group health check (every 30s per task)
        // - OPTIONS preflight: every cross-origin request fires one
        options.GetLevel = (httpContext, _, ex) =>
        {
            if (ex != null) return LogEventLevel.Error;
            if (httpContext.Response.StatusCode >= 500) return LogEventLevel.Error;
            if (HttpMethods.IsOptions(httpContext.Request.Method)) return LogEventLevel.Verbose;
            var path = httpContext.Request.Path.Value;
            if (path is "/livecheck" or "/version") return LogEventLevel.Verbose;
            return LogEventLevel.Information;
        };
    });
    app.UseMiddleware<NewRelicRequestFilter>();
    app.UseMiddleware<NewRelicEcsEnricher>();
    app.UseOpenApi();
    app.UseSwaggerUi(config =>
    {
        config.DocumentTitle = "GATool API";
        config.Path = "/swagger";
        config.DocumentPath = "/swagger/{documentName}/swagger.json";
    });
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}