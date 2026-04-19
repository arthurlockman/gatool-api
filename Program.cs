using Amazon.DynamoDBv2;
using Amazon.S3;
using Amazon.SecretsManager;
using GAToolAPI.Attributes;
using GAToolAPI.AuthExtensions;
using GAToolAPI.Helpers;
using GAToolAPI.Jobs;
using GAToolAPI.Middleware;
using GAToolAPI.Services;
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
        "Auth0Issuer", "Auth0Audience",
        "FRCApiKey", "TBAApiKey", "FTCApiKey", "CasterstoolApiKey", "TOAApiKey",
        "FRCCurrentSeason", "FTCCurrentSeason",
        "MailChimpAPIKey", "MailchimpAPIURL", "MailchimpListID",
        "Auth0AdminClientId", "Auth0AdminClientSecret",
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
        .MinimumLevel.Override("Microsoft.AspNetCore.Routing", LogEventLevel.Warning));

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Authority = secretProvider.GetSecret("Auth0Issuer");
            options.Audience = secretProvider.GetSecret("Auth0Audience");
        });
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("user", policy => policy.Requirements.Add(new HasRoleRequirement("user")))
        .AddPolicy("admin", policy => policy.Requirements.Add(new HasRoleRequirement("admin")));
    builder.Services.AddSingleton<IAuthorizationHandler, HasRoleHandler>();

    builder.Services.AddSingleton<ISecretProvider>(secretProvider);
    builder.Services.AddSingleton<IAmazonSecretsManager>(smClient);
    builder.Services.AddAWSService<IAmazonS3>();
    builder.Services.AddAWSService<IAmazonDynamoDB>();

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(b => b
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
    });

    builder.Services.AddControllers();

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
    app.UseSerilogRequestLogging();
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