using Chubb.Claims.Application;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Chubb.Claims.Api.Diagnostics;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception error, CancellationToken ct)
    {
        var status = error switch
        {
            DomainValidationException or BadHttpRequestException => 400,
            ClaimAccessDeniedException => 403,
            ClaimConflictException => 409,
            _ => 500
        };
        if (status == 500)
            logger.LogError(error, "Unhandled request failure with correlation {CorrelationId}", context.TraceIdentifier);
        else
            logger.LogWarning("Request rejected with {StatusCode} and correlation {CorrelationId}", status, context.TraceIdentifier);

        context.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = status switch { 400 => "Invalid request", 403 => "Action not permitted", 409 => "Claim changed or action conflicts", _ => "Unexpected failure" },
            Detail = status switch
            {
                500 => "The request could not be completed. Use the correlation ID when investigating.",
                _ when error is BadHttpRequestException => "The request body or parameters are invalid.",
                _ => error.Message
            }
        };
        problem.Extensions["correlationId"] = context.TraceIdentifier;
        await context.Response.WriteAsJsonAsync(problem, options: (System.Text.Json.JsonSerializerOptions?)null,
            contentType: "application/problem+json", cancellationToken: ct);
        return true;
    }
}
