using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Chubb.Claims.Api.Tests;

// EN-03: TestServer verifies documentation HTTP responses. These tests do not
// open a browser or claim that the Swagger interface was visually exercised.
public sealed class SwaggerDocumentationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Development_serves_swagger_index_and_its_local_javascript_bundle()
    {
        using var client = factory.As(null);
        var index = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        Assert.Equal("text/html", index.Content.Headers.ContentType?.MediaType);
        var html = await index.Content.ReadAsStringAsync();
        Assert.Contains("swagger-ui", html, StringComparison.OrdinalIgnoreCase);

        // Swashbuckle serves its endpoint configuration in the initializer asset,
        // rather than embedding that configuration in the index HTML.
        var initializer = await client.GetAsync("/swagger/index.js");
        Assert.Equal(HttpStatusCode.OK, initializer.StatusCode);
        Assert.Contains("/openapi/v1.json", await initializer.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var bundle = await client.GetAsync("/swagger/swagger-ui-bundle.js");
        Assert.Equal(HttpStatusCode.OK, bundle.StatusCode);
        Assert.Contains("SwaggerUIBundle", await bundle.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Openapi_describes_demo_header_for_protected_operations_without_securing_public_metadata()
    {
        using var client = factory.As(null);
        var document = await ReadDocument(client);
        var scheme = document["components"]?["securitySchemes"]?["DemoIdentity"];
        Assert.NotNull(scheme);
        Assert.Equal("apiKey", scheme!["type"]!.GetValue<string>());
        Assert.Equal("X-Demo-User", scheme["name"]!.GetValue<string>());
        Assert.Equal("header", scheme["in"]!.GetValue<string>());

        var protectedOperations = new[]
        {
            ("/api/claims", "post"), ("/api/claims", "get"),
            ("/api/officer/claims", "get"), ("/api/claims/{id}", "get"),
            ("/api/claims/{id}/history", "get"), ("/api/claims/{id}/notifications", "get"),
            ("/api/claims/{id}/assignment", "post"), ("/api/claims/{id}/decision", "post")
        };
        foreach (var (path, method) in protectedOperations)
        {
            var operation = document["paths"]?[path]?[method];
            Assert.NotNull(operation);
            var requirements = Assert.IsType<JsonArray>(operation!["security"]);
            Assert.Contains(requirements, requirement => requirement?["DemoIdentity"] is JsonArray);
        }

        foreach (var path in new[] { "/api/demo/users", "/api/demo/markets", "/health/live" })
        {
            var operation = document["paths"]?[path]?["get"];
            Assert.NotNull(operation);
            var requirements = operation!["security"] as JsonArray;
            Assert.True(requirements is null || requirements.Count == 0,
                $"Public operation {path} must not require a demo identity.");
        }
        var globalRequirements = document["security"] as JsonArray;
        Assert.True(globalRequirements is null || globalRequirements.Count == 0,
            "Operation-specific identity requirements must not implicitly secure public metadata.");
    }

    [Fact]
    public async Task Openapi_contains_submission_body_created_response_and_read_response_schemas()
    {
        using var client = factory.As(null);
        var document = await ReadDocument(client);
        var submission = document["paths"]?["/api/claims"]?["post"];
        Assert.NotNull(submission);
        var body = ResolveSchema(document, submission!["requestBody"]?["content"]?["application/json"]?["schema"]);
        var properties = Assert.IsType<JsonObject>(body["properties"]);
        foreach (var field in new[] { "type", "incidentDate", "description", "market", "currency", "claimedAmount" })
            Assert.True(properties.ContainsKey(field), $"Submission schema is missing {field}.");

        var created = ResolveSchema(document, submission["responses"]?["201"]?["content"]?["application/json"]?["schema"]);
        var createdProperties = Assert.IsType<JsonObject>(created["properties"]);
        foreach (var field in new[] { "id", "reference", "status", "rowVersion", "claimedAmount", "currency" })
            Assert.True(createdProperties.ContainsKey(field), $"Created claim schema is missing {field}.");

        var list = ResolveSchema(document,
            document["paths"]?["/api/claims"]?["get"]?["responses"]?["200"]?["content"]?["application/json"]?["schema"]);
        var listProperties = Assert.IsType<JsonObject>(list["properties"]);
        Assert.True(listProperties.ContainsKey("items"));
        Assert.True(listProperties.ContainsKey("total"));
    }

    [Fact]
    public async Task Production_hides_documentation_but_the_host_still_serves_liveness()
    {
        await using var production = new ApiFactory { EnvironmentName = Environments.Production };
        using var client = production.As(null);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        foreach (var path in new[] { "/swagger/index.html", "/swagger/swagger-ui-bundle.js", "/openapi/v1.json" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    }

    private static async Task<JsonObject> ReadDocument(HttpClient client)
    {
        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<JsonObject>(await response.Content.ReadFromJsonAsync<JsonNode>());
    }

    private static JsonObject ResolveSchema(JsonObject document, JsonNode? schema)
    {
        Assert.NotNull(schema);
        var current = schema!;
        // The application uses local component references; resolving those verifies
        // useful fields rather than accepting a nonempty $ref as sufficient evidence.
        for (var depth = 0; current["$ref"] is JsonValue reference; depth++)
        {
            Assert.True(depth < 10, "Unexpected recursive schema reference.");
            var pointer = reference.GetValue<string>();
            Assert.StartsWith("#/", pointer, StringComparison.Ordinal);
            JsonNode? resolved = document;
            foreach (var segment in pointer[2..].Split('/'))
                resolved = resolved?[segment.Replace("~1", "/").Replace("~0", "~")];
            Assert.NotNull(resolved);
            current = resolved!;
        }
        return Assert.IsType<JsonObject>(current);
    }
}
