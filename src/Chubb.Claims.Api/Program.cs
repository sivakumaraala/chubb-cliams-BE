using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Chubb.Claims.Api.Diagnostics;
using Chubb.Claims.Api.Security;
using Chubb.Claims.Application;
using Chubb.Claims.Infrastructure;
using Chubb.Claims.Persistence;
using Microsoft.AspNetCore.Authentication;
using Serilog;
using Serilog.Context;

var builder = WebApplication.CreateBuilder(args);
LoggingConfiguration.ResolveFilePaths(builder.Configuration, builder.Environment.ContentRootPath);
builder.Host.UseSerilog((context, services, log) => log.ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

// Local Windows fallback also lets database-free host tests start without a connection secret.
builder.Configuration["ConnectionStrings:ClaimsDatabase"] ??=
    "Server=(localdb)\\MSSQLLocalDB;Database=ChubbClaims;Integrated Security=True;TrustServerCertificate=True";

if (!builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("DemoIdentity:Enabled"))
    throw new InvalidOperationException("Demo identity can only be enabled in Development.");

builder.Services.AddClaimsPersistence(builder.Configuration);
builder.Services.AddClaimsInfrastructure(builder.Configuration);
builder.Services.AddScoped<SubmitClaimHandler>();
builder.Services.AddScoped<AssignClaimHandler>();
builder.Services.AddScoped<DecideClaimHandler>();
builder.Services.AddScoped<RequestInformationHandler>();
builder.Services.AddScoped<ProvideInformationHandler>();
builder.Services.AddScoped<RecordSettlementPaymentHandler>();
builder.Services.AddScoped<ListClaimsHandler>();
builder.Services.AddScoped<GetClaimHandler>();
builder.Services.AddScoped<GetClaimHistoryHandler>();
builder.Services.AddScoped<GetClaimNotificationsHandler>();
builder.Services.AddScoped<GetOfficerDashboardHandler>();
builder.Services.AddSingleton<DemoMarketCatalog>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddAuthentication(DemoUsers.Scheme)
    .AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>(DemoUsers.Scheme, _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Claimant", policy => policy.RequireRole("Claimant"));
    options.AddPolicy("Officer", policy => policy.RequireRole("Officer"));
});
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    options.JsonSerializerOptions.RespectRequiredConstructorParameters = true;
});
builder.Services.AddOpenApi(DemoOpenApiConfiguration.Configure);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddCors(options => options.AddPolicy("AngularLocal", policy =>
    policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.Use(async (context, next) =>
{
    var supplied = context.Request.Headers["X-Correlation-ID"].ToString();
    context.TraceIdentifier = Regex.IsMatch(supplied, "^[A-Za-z0-9_.-]{1,64}$") ? supplied : Guid.NewGuid().ToString("N");
    context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
    using (LogContext.PushProperty("CorrelationId", context.TraceIdentifier)) await next();
});
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) app.UseCors("AngularLocal");
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.SwaggerEndpoint("/openapi/v1.json", "Claims Assessment API v1");
        options.DocumentTitle = "Claims Assessment API";
        options.DisplayRequestDuration();
        options.EnableTryItOutByDefault();
        options.ConfigObject.ValidatorUrl = null;
    });
}

app.MapControllers();
if (app.Configuration.GetValue("Database:InitializeOnStartup", true))
    await app.Services.InitializeClaimsDatabaseAsync();
await app.RunAsync();

public partial class Program;
