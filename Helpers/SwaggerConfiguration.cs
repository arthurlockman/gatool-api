using System.Text.Json.Nodes;
using NJsonSchema;
using NJsonSchema.Generation.TypeMappers;
using NSwag;
using NSwag.Generation.Processors.Security;

namespace GAToolAPI.Helpers;

public static class SwaggerConfiguration
{
    public static IServiceCollection AddGatoolOpenApi(this IServiceCollection services)
    {
        return services.AddOpenApiDocument(config =>
        {
            config.Title = "GATool API";
            config.Version = "v3";
            config.Description =
                "FRC, FTC, and FIRST Global competition data and GATool services. " +
                "Protected operations require an Authorization header containing the Bearer scheme and an access token. " +
                "Malformed requests can return HTTP 400. External-service failures preserve the upstream HTTP status " +
                "and return an error object; unexpected failures return HTTP 500.";

            // JsonNode implements collection interfaces, but its wire format is JSON, not key/value pairs.
            config.SchemaSettings.TypeMappers.Add(new PrimitiveTypeMapper(typeof(JsonObject), schema =>
            {
                schema.Type = JsonObjectType.Object;
                schema.AllowAdditionalProperties = true;
            }));
            config.SchemaSettings.TypeMappers.Add(new PrimitiveTypeMapper(typeof(JsonNode), _ => { }));

            config.AddSecurity("JWT", [], new OpenApiSecurityScheme
            {
                Type = OpenApiSecuritySchemeType.ApiKey,
                Name = "Authorization",
                In = OpenApiSecurityApiKeyLocation.Header,
                Description = "Enter the Bearer scheme followed by a space and your GATool access token."
            });

            config.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("JWT"));
            config.OperationProcessors.Add(new SwaggerOperationProcessor());
        });
    }
}
