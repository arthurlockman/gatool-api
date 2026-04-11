using Amazon.DynamoDBv2;
using Amazon.S3;
using Amazon.SecretsManager;
using GAToolAPI.Attributes;
using GAToolAPI.AuthExtensions;
using GAToolAPI.Jobs;
using GAToolAPI.Middleware;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using NewRelic.LogEnrichers.Serilog;
using NSwag;
using NSwag.Generation.Processors.Security;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithNewRelicLogsInContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

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
        AllowAdmin = true
    };
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfig));

    builder.Services.AddHttpClient();

    builder.Services.AddSingleton<FRCApiService>();
    builder.Services.AddSingleton<FirstGlobalApiService>();
    builder.Services.AddSingleton<TBAApiService>();
    builder.Services.AddSingleton<StatboticsApiService>();
    builder.Services.AddSingleton<CasterstoolApiService>();
    builder.Services.AddSingleton<FTCApiService>();
    builder.Services.AddSingleton<FTCScoutApiService>();
    builder.Services.AddSingleton<TOAApiService>();
    builder.Services.AddSingleton<UserStorageService>();
    builder.Services.AddSingleton<HighScoreRepository>();
    builder.Services.AddSingleton<TeamDataService>();
    builder.Services.AddSingleton<ScheduleService>();
    builder.Services.AddSingleton<FTCScheduleService>();
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
    app.UseSerilogRequestLogging();
    app.UseMiddleware<NewRelicRequestFilter>();
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