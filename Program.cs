using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using GAToolAPI.Attributes;
using GAToolAPI.AuthExtensions;
using GAToolAPI.Jobs;
using GAToolAPI.Middleware;
using GAToolAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Azure;
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

    var keyVaultUrl = builder.Configuration["KeyVaultUrl"] ??
                      throw new ArgumentException("Key Vault URL required to start up.");
    var keyVaultClient = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());

    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .MinimumLevel.Override("Microsoft.AspNetCore.Hosting", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore.Mvc", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore.Routing", LogEventLevel.Warning));

    var authAuthority = keyVaultClient.GetSecret("Auth0Issuer");
    var authAudience = keyVaultClient.GetSecret("Auth0Audience");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Authority = authAuthority.Value.Value;
            options.Audience = authAudience.Value.Value;
        });
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy("user", policy => policy.Requirements.Add(new HasRoleRequirement("user")))
        .AddPolicy("admin", policy => policy.Requirements.Add(new HasRoleRequirement("admin")));
    builder.Services.AddSingleton<IAuthorizationHandler, HasRoleHandler>();

    var storageConnectionString = keyVaultClient.GetSecret("UserStorageConnectionString").Value.Value;
    builder.Services.AddAzureClients(clientBuilder =>
    {
        // Register default credential for all DI services
        DefaultAzureCredential credential = new();
        clientBuilder.UseCredential(credential);

        // Register clients
        clientBuilder.AddSecretClient(new Uri(keyVaultUrl));
        clientBuilder.AddBlobServiceClient(storageConnectionString);
    });

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
    builder.Services.AddSingleton<TBAApiService>();
    builder.Services.AddSingleton<StatboticsApiService>();
    builder.Services.AddSingleton<FTCApiService>();
    builder.Services.AddSingleton<FTCScoutApiService>();
    builder.Services.AddSingleton<TOAApiService>();
    builder.Services.AddSingleton<UserStorageService>();
    builder.Services.AddSingleton<TeamDataService>();
    builder.Services.AddSingleton<ScheduleService>();
    builder.Services.AddSingleton<FTCScheduleService>();

    // Register job services
    builder.Services.AddScoped<JobRunnerService>();
    builder.Services.AddScoped<UpdateGlobalHighScoresJob>();
    builder.Services.AddScoped<SyncUsersJob>();

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
