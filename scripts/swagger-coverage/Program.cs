using System.Text.Json.Nodes;
using System.Xml.Linq;
using GAToolAPI.Controllers;
using GAToolAPI.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSwag.AspNetCore;
using NSwag.Generation.AspNetCore;

// Discover real routes without starting Program.cs, hosted services, or external clients.
var builder = WebApplication.CreateBuilder();
builder.Services.AddControllers().AddApplicationPart(typeof(FrcApiController).Assembly);
builder.Services.AddGatoolOpenApi();
await using var app = builder.Build();
app.MapControllers();

var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
    .OfType<RouteEndpoint>()
    .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() != null)
    .ToArray();
var descriptions = app.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
    .ApiDescriptionGroups.Items.SelectMany(group => group.Items).ToArray();
var registration = app.Services.GetServices<OpenApiDocumentRegistration>().Single();
var document = await new AspNetCoreOpenApiDocumentGenerator(registration.Settings).GenerateAsync(app.Services);
var json = JsonNode.Parse(document.ToJson())!;
var paths = json["paths"]!.AsObject();
var failures = new List<string>();
var expectedOperations = new HashSet<string>(StringComparer.Ordinal);
var operationIds = new HashSet<string>(StringComparer.Ordinal);
var xmlPath = Path.ChangeExtension(typeof(FrcApiController).Assembly.Location, ".xml");
var members = XDocument.Load(xmlPath).Descendants("member").ToArray();

foreach (var endpoint in endpoints)
{
    var action = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()!;
    var path = "/" + string.Join("/", endpoint.RoutePattern.PathSegments.Select(segment =>
        string.Concat(segment.Parts.Select(part => part switch
        {
            Microsoft.AspNetCore.Routing.Patterns.RoutePatternParameterPart parameter => "{" + parameter.Name + "}",
            Microsoft.AspNetCore.Routing.Patterns.RoutePatternLiteralPart literal => literal.Content,
            Microsoft.AspNetCore.Routing.Patterns.RoutePatternSeparatorPart separator => separator.Content,
            _ => throw new InvalidOperationException($"Unsupported route part: {part}")
        }))));
    var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
        ?? throw new InvalidOperationException($"No HTTP methods for {path}");
    foreach (var method in methods)
    {
        var key = $"{method} {path}";
        if (!expectedOperations.Add(key)) failures.Add($"Duplicate registered operation: {key}");
        var description = descriptions.SingleOrDefault(item =>
            item.ActionDescriptor.Id == action.Id && item.HttpMethod == method);
        if (description == null)
        {
            failures.Add($"Registered route missing from ApiExplorer: {key}");
            continue;
        }
        var operation = paths[path]?[method.ToLowerInvariant()];
        if (operation == null)
        {
            failures.Add($"Registered route missing from OpenAPI: {key}");
            continue;
        }
        if (string.IsNullOrWhiteSpace(operation["summary"]?.GetValue<string>()))
            failures.Add($"Missing operation summary: {key}");
        if (operation["operationId"]?.GetValue<string>() is not { Length: > 0 } operationId ||
            !operationIds.Add(operationId))
            failures.Add($"Missing or duplicate operation ID: {key}");
        var parameters = operation["parameters"]?.AsArray() ?? [];
        foreach (var parameter in description.ParameterDescriptions.Where(p => p.Source.Id is "Path" or "Query"))
        {
            var location = parameter.Source.Id.ToLowerInvariant();
            var documented = parameters.SingleOrDefault(p =>
                p?["name"]?.GetValue<string>() == parameter.Name && p?["in"]?.GetValue<string>() == location);
            if (documented == null || string.IsNullOrWhiteSpace(documented["description"]?.GetValue<string>()))
                failures.Add($"Missing {location} parameter documentation for {parameter.Name}: {key}");
            if (location == "path" && documented?["required"]?.GetValue<bool>() != true)
                failures.Add($"Path parameter not required: {parameter.Name}: {key}");
        }
        foreach (var parameter in description.ParameterDescriptions.Where(p => p.Source.Id == "Body"))
        {
            if (operation["requestBody"] == null)
                failures.Add($"Missing request body: {key}");
            if (string.IsNullOrWhiteSpace(operation["requestBody"]?["description"]?.GetValue<string>()))
                failures.Add($"Missing request body description: {key}");
        }
        foreach (var response in operation["responses"]!.AsObject())
        {
            if (string.IsNullOrWhiteSpace(response.Value?["description"]?.GetValue<string>()))
                failures.Add($"Missing {response.Key} response description: {key}");
        }
        if (!operation["responses"]!.AsObject().Any(response => response.Key.StartsWith('2')))
            failures.Add($"No successful response documented: {key}");

        var memberPrefix = $"M:{action.MethodInfo.DeclaringType!.FullName}.{action.MethodInfo.Name}";
        var member = members.SingleOrDefault(m =>
        {
            var name = m.Attribute("name")?.Value;
            return name == memberPrefix ||
                name?.StartsWith(memberPrefix + "(", StringComparison.Ordinal) == true &&
                name[(memberPrefix.Length + 1)..^1].Split(',').Length == action.MethodInfo.GetParameters().Length;
        });
        if (member == null) failures.Add($"Missing XML action comments: {key}");
        else
        {
            if (string.IsNullOrWhiteSpace(member.Element("summary")?.Value))
                failures.Add($"Missing XML summary: {key}");
            var responses = operation["responses"]!.AsObject();
            var documentedStatuses = member.Elements("response")
                .Select(response => response.Attribute("code")!.Value).ToHashSet(StringComparer.Ordinal);
            foreach (var response in member.Elements("response"))
            {
                var status = response.Attribute("code")!.Value;
                if (!responses.ContainsKey(status)) failures.Add($"XML response {status} missing from OpenAPI: {key}");
            }
            foreach (var status in responses.Select(response => response.Key).Except(documentedStatuses))
                failures.Add($"OpenAPI response {status} missing from XML comments: {key}");
        }
        var castersTool = action.MethodInfo.Name == "GetMatchupConnections" &&
            action.ControllerTypeInfo == typeof(FrcApiController);
        if ((operation["deprecated"]?.GetValue<bool>() == true) != castersTool)
            failures.Add($"Incorrect Casters Tool deprecation: {key}");
    }
}

var actualOperations = paths.SelectMany(path => path.Value!.AsObject()
    .Where(item => item.Key is "get" or "post" or "put" or "delete" or "patch" or "head" or "options")
    .Select(item => $"{item.Key.ToUpperInvariant()} {path.Key}")).ToHashSet(StringComparer.Ordinal);
foreach (var extra in actualOperations.Except(expectedOperations))
    failures.Add($"OpenAPI operation not registered: {extra}");
if (expectedOperations.Count == 0) failures.Add("No registered operations were discovered.");

foreach (var path in new[] { "/v3/{year}/teams", "/ftc/v2/{year}/teams" })
{
    var operation = paths[path]!["get"]!;
    var eventCode = operation["parameters"]!.AsArray()
        .Single(parameter => parameter!["name"]!.GetValue<string>() == "eventCode")!;
    Require(eventCode["in"]!.GetValue<string>() == "query" &&
        eventCode["required"]?.GetValue<bool>() != true &&
        eventCode["schema"]!["type"]!.GetValue<string>() == "string",
        $"Event team list must accept an optional eventCode string query parameter: {path}");
    Require(operation["description"]?.GetValue<string>().Contains("?eventCode=", StringComparison.Ordinal) == true,
        $"Event team list must include a concrete query example: {path}");
}

foreach (var path in new[]
{
    "/v3/{year}/scores/{eventCode}/{tournamentLevel}",
    "/v3/{year}/scores/{eventCode}/{tournamentLevel}/{start}/{end}"
})
{
    var alternatives = paths[path]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["anyOf"]!.AsArray();
    Require(alternatives.Select(schema => schema!["$ref"]!.GetValue<string>()).ToHashSet().SetEquals(
        ["#/components/schemas/MatchScoresResponse", "#/components/schemas/GenericMatchScoresResponse"]),
        $"Both seasonal score schemas must be documented: {path}");
}

var webhook = paths["/webhooks/mailchimp"]!["post"]!;
var secret = webhook["parameters"]!.AsArray().Single(parameter => parameter!["name"]!.GetValue<string>() == "secret")!;
Require(secret["in"]!.GetValue<string>() == "query" && secret["required"]!.GetValue<bool>() &&
    secret["schema"]!["type"]!.GetValue<string>() == "string", "Webhook requires a documented secret query parameter.");
var webhookForm = webhook["requestBody"]!["content"]!["application/x-www-form-urlencoded"]!["schema"]!;
Require(webhookForm["required"]!.AsArray().Select(field => field!.GetValue<string>())
    .ToHashSet().SetEquals(["type", "data[email]"]), "Webhook must require event type and subscriber email form fields.");
Require(webhookForm["properties"]!["data[merges][GATOOL]"] != null, "Webhook merge field must be documented.");
Require(paths["/webhooks/mailchimp"]!["get"]!["responses"]!["200"]!["content"] == null,
    "Webhook validation returns an empty response, not a binary file.");
Require(paths["/v3/{year}/avatars/team/{teamNumber}/avatar.png"]!["get"]!["responses"]!["200"]!["content"]!["image/png"] != null,
    "Team avatar response must use image/png.");

foreach (var path in new[] { "/v3/team/{teamNumber}/updates/history", "/ftc/v2/team/{teamNumber}/updates/history",
    "/v3/{year}/communityUpdates/{eventCode}", "/ftc/v2/{year}/communityUpdates/{eventCode}", "/v3/{year}/highscores" })
    Require(paths[path]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["type"]!.GetValue<string>() == "array",
        $"Response must be an array: {path}");
Require(paths["/ftc/v2/{year}/teams"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["type"]!.GetValue<string>() == "object",
    "Raw JsonObject responses must be JSON objects, not key/value arrays.");
Require(paths["/v3/firstglobal/{year}/queryAwards"]!["post"]!["requestBody"]!["required"]?.GetValue<bool>() != true,
    "FIRST Global awards filter body must remain optional.");
Require(paths["/v3/user/preferences"]!["put"]!["responses"]!["200"] == null,
    "Storing preferences must not advertise HTTP 200.");

CheckReferences(json);

Console.WriteLine($"Registered operations: {expectedOperations.Count}; ApiExplorer descriptions: {descriptions.Length}; OpenAPI operations: {actualOperations.Count}.");
foreach (var failure in failures) Console.Error.WriteLine(failure);
if (args.Length == 2 && args[0] == "--output")
    await File.WriteAllTextAsync(args[1], document.ToJson());
if (failures.Count > 0)
{
    Console.Error.WriteLine($"FAIL: {failures.Count} documentation coverage errors.");
    return 1;
}
Console.WriteLine("PASS: routes, summaries, parameters, bodies, responses, XML comments, and Casters Tool deprecation.");
Console.WriteLine("PASS: event-team queries, seasonal score schemas, raw JSON/array shapes, PNG, webhook metadata, and local references.");
return 0;

void Require(bool condition, string message)
{
    if (!condition) failures.Add(message);
}

void CheckReferences(JsonNode? node)
{
    if (node is JsonObject obj)
    {
        if (obj["$ref"]?.GetValue<string>() is { } reference)
        {
            JsonNode? target = json;
            Require(reference.StartsWith("#/", StringComparison.Ordinal), $"Unexpected external schema reference: {reference}");
            foreach (var segment in reference[2..].Split('/'))
            {
                var name = segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
                target = target switch
                {
                    JsonObject parent => parent[name],
                    JsonArray array when int.TryParse(name, out var index) && index >= 0 && index < array.Count => array[index],
                    _ => null
                };
            }
            Require(target != null, $"Unresolvable schema reference: {reference}");
        }
        foreach (var property in obj) CheckReferences(property.Value);
    }
    else if (node is JsonArray array)
        foreach (var item in array) CheckReferences(item);
}
