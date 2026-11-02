using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Chubb.Claims.Api.Tests;

public sealed class AccessAndMetadataTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Demo_users_are_explicitly_available_for_both_roles()
    {
        using var client = factory.As(null);
        var response = await client.GetAsync("/api/demo/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = Assert.IsType<JsonArray>(await response.Content.ReadFromJsonAsync<JsonNode>());
        var ids = users.Select(x => x!["id"]!.GetValue<string>()).ToArray();
        Assert.Contains("claimant-alex", ids);
        Assert.Contains("claimant-blair", ids);
        Assert.Contains("officer-lee", ids);
        Assert.Contains("officer-sam", ids);
        Assert.All(users, user => Assert.Contains(user!["role"]!.GetValue<string>(), new[] { "Claimant", "Officer" }));
    }

    [Fact]
    public async Task Markets_are_six_explicit_demo_values_with_currencies()
    {
        using var client = factory.As(null);
        var response = await client.GetAsync("/api/demo/markets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var markets = Assert.IsType<JsonArray>(await response.Content.ReadFromJsonAsync<JsonNode>());
        Assert.Equal(6, markets.Count);
        Assert.Equal(6, markets.Select(x => x!["code"]!.GetValue<string>()).Distinct().Count());
        Assert.All(markets, market => Assert.NotEmpty(Assert.IsType<JsonArray>(market!["currencies"])));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unknown-identity")]
    public async Task Missing_or_unknown_demo_identity_cannot_read_claims(string? identity)
    {
        using var client = factory.As(identity);
        var response = await client.GetAsync("/api/claims");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Claimant_cannot_open_officer_queue()
    {
        using var client = factory.As("claimant-alex");
        var response = await client.GetAsync("/api/officer/claims");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Officer_cannot_submit_a_claim_as_a_claimant()
    {
        using var client = factory.As("officer-lee");
        var response = await client.PostAsJsonAsync("/api/claims", new
        {
            type = "Motor", incidentDate = "2026-01-01", description = "Demo incident",
            market = "M01", currency = "SGD", claimedAmount = 1000m
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("amount-zero")]
    [InlineData("amount-precision")]
    [InlineData("unknown-market")]
    [InlineData("unsupported-currency")]
    [InlineData("empty-description")]
    [InlineData("future-incident")]
    [InlineData("unknown-type")]
    [InlineData("numeric-type")]
    public async Task Invalid_submission_is_rejected_before_persistence(string invalidCase)
    {
        using var client = factory.As("claimant-alex");
        var body = new Dictionary<string, object>
        {
            ["type"] = "Motor", ["incidentDate"] = "2026-01-01", ["description"] = "Demo incident",
            ["market"] = "M01", ["currency"] = "SGD", ["claimedAmount"] = 1000m
        };
        switch (invalidCase)
        {
            case "amount-zero": body["claimedAmount"] = 0m; break;
            case "amount-precision": body["claimedAmount"] = 1000.001m; break;
            case "unknown-market": body["market"] = "UNKNOWN"; break;
            case "unsupported-currency": body["currency"] = "USD"; break;
            case "empty-description": body["description"] = " "; break;
            case "future-incident": body["incidentDate"] = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd"); break;
            case "unknown-type": body["type"] = "Unknown"; break;
            case "numeric-type": body["type"] = 1; break;
        }

        var response = await client.PostAsJsonAsync("/api/claims", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Missing_required_submission_field_is_rejected_instead_of_defaulted()
    {
        using var client = factory.As("claimant-alex");
        var response = await client.PostAsJsonAsync("/api/claims", new
        {
            incidentDate = "2026-01-01", description = "Demo incident",
            market = "M01", currency = "SGD", claimedAmount = 1000m
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public async Task Invalid_pagination_is_rejected_before_querying_the_database(int page, int pageSize)
    {
        using var client = factory.As("officer-lee");
        var response = await client.GetAsync($"/api/officer/claims?page={page}&pageSize={pageSize}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
