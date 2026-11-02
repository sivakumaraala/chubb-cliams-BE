using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Chubb.Claims.Api.Security;

public sealed record DemoUser(string Id, string DisplayName, string Role);

public static class DemoUsers
{
    public const string Scheme = "Demo";
    public static readonly DemoUser[] All =
    [
        new("claimant-alex", "Alex (demo claimant)", "Claimant"),
        new("claimant-blair", "Blair (demo claimant)", "Claimant"),
        new("officer-lee", "Lee (demo officer)", "Officer"),
        new("officer-sam", "Sam (demo officer)", "Officer")
    ];

    public static string ActorId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("An authenticated actor is required.");
}

// US-01: development-only impersonation, deliberately not production authentication.
public sealed class DemoAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, IWebHostEnvironment environment, IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("DemoIdentity:Enabled"))
            return Task.FromResult(AuthenticateResult.Fail("Demo identity is disabled."));
        if (!Request.Headers.TryGetValue("X-Demo-User", out var ids))
            return Task.FromResult(AuthenticateResult.NoResult());
        var user = ids.Count == 1 ? DemoUsers.All.SingleOrDefault(x => x.Id == ids[0]) : null;
        if (user is null)
            return Task.FromResult(AuthenticateResult.Fail("Unknown demo identity."));
        var claims = new[]
        {
            new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, user.Id),
            new System.Security.Claims.Claim(ClaimTypes.Name, user.DisplayName),
            new System.Security.Claims.Claim(ClaimTypes.Role, user.Role)
        };
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, DemoUsers.Scheme)), DemoUsers.Scheme)));
    }
}
