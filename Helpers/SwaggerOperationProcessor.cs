using GAToolAPI.Controllers;
using GAToolAPI.Models;
using NJsonSchema;
using NSwag;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace GAToolAPI.Helpers;

public sealed class SwaggerOperationProcessor : IOperationProcessor
{
    public bool Process(OperationProcessorContext context)
    {
        var operation = context.OperationDescription.Operation;
        if (context.MethodInfo.DeclaringType == typeof(FrcApiController))
        {
            if (context.MethodInfo.Name == nameof(FrcApiController.GetTeamAvatar))
            {
                operation.Responses["200"].Content.Clear();
                operation.Responses["200"].Content.Add("image/png", new OpenApiMediaType
                {
                    Schema = new JsonSchema { Type = JsonObjectType.String, Format = "binary" }
                });
            }
            else if (context.MethodInfo.Name == nameof(FrcApiController.GetScores))
            {
                // Duplicate response-status attributes otherwise retain only the last seasonal schema.
                var scores = new JsonSchema();
                foreach (var type in new[] { typeof(MatchScoresResponse), typeof(GenericMatchScoresResponse) })
                {
                    var schema = context.SchemaGenerator.Generate(type, context.SchemaResolver);
                    context.Document.Definitions.TryAdd(type.Name, schema);
                    scores.AnyOf.Add(new JsonSchema { Reference = schema });
                }
                operation.Responses["200"].Content["application/json"].Schema = scores;
            }
        }

        if (context.MethodInfo.DeclaringType == typeof(WebhookController) &&
            context.MethodInfo.Name == nameof(WebhookController.ValidateWebhook))
            operation.Responses["200"].Content.Clear();

        if (context.MethodInfo.DeclaringType != typeof(WebhookController) ||
            context.MethodInfo.Name != nameof(WebhookController.HandleWebhook))
            return true;

        // These inputs are read from Request directly, so ApiExplorer cannot discover them.
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "secret",
            Kind = OpenApiParameterKind.Query,
            Schema = new JsonSchema { Type = JsonObjectType.String },
            IsRequired = true,
            Description = "Shared Mailchimp webhook secret. Required for POST, not for URL validation with GET."
        });
        var form = new JsonSchema { Type = JsonObjectType.Object };
        form.Properties.Add("type", new JsonSchemaProperty
        {
            Type = JsonObjectType.String,
            Description = "Mailchimp event type, for example subscribe, unsubscribe, profile, or cleaned."
        });
        form.Properties.Add("data[email]", new JsonSchemaProperty
        {
            Type = JsonObjectType.String,
            Description = "Subscriber email address."
        });
        form.Properties.Add("data[merges][GATOOL]", new JsonSchemaProperty
        {
            Type = JsonObjectType.String,
            Description = "Optional GATOOL merge-field value."
        });
        form.RequiredProperties.Add("type");
        form.RequiredProperties.Add("data[email]");
        operation.RequestBody = new OpenApiRequestBody
        {
            IsRequired = true,
            Description = "Mailchimp event form. Field names use the literal bracket notation shown."
        };
        operation.RequestBody.Content.Add("application/x-www-form-urlencoded", new OpenApiMediaType { Schema = form });
        return true;
    }
}
