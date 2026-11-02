using Chubb.Claims.Api.Security;
using Chubb.Claims.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chubb.Claims.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/demo")]
public sealed class DemoController(IWebHostEnvironment environment, IConfiguration configuration,
    DemoMarketCatalog catalog) : ControllerBase
{
    private bool Enabled => environment.IsDevelopment() && configuration.GetValue<bool>("DemoIdentity:Enabled");

    [HttpGet("users")]
    public IActionResult Users() => Enabled ? Ok(DemoUsers.All) : NotFound();

    [HttpGet("markets")]
    public IActionResult Markets() => Enabled ? Ok(catalog.Markets) : NotFound();
}
