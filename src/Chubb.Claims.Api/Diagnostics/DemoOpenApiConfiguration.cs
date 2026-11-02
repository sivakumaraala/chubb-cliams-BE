using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Chubb.Claims.Api.Diagnostics;

public static class DemoOpenApiConfiguration
{
    private const string IdentityScheme = "DemoIdentity";

    public static void Configure(OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = "Claims Assessment API",
                Version = "v1",
                Description = "Local development assessment. Use Authorize with a demo identity such as " +
                    "claimant-alex, claimant-blair, officer-lee, or officer-sam. " +
                    "The X-Demo-User header is development impersonation, not production authentication. " +
                    "Use the latest rowVersion from a claim when assigning it or recording a decision."
            };
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[IdentityScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "X-Demo-User",
                Description = "Demo identity ID, e.g. claimant-alex or officer-lee. Development only; do not enter a real token."
            };
            return Task.CompletedTask;
        });

        options.AddOperationTransformer((operation, context, _) =>
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;
            if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
            {
                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(IdentityScheme, context.Document)] = []
                });
                operation.Responses ??= new OpenApiResponses();
                operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Missing or unknown demo identity." });
                operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Role or assigned-officer restriction." });
            }
            return Task.CompletedTask;
        });
    }
}
