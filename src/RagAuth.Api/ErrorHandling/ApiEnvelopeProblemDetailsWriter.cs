using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using RagAuth.Api.Contracts;
using RagAuth.Api.Observability;

namespace RagAuth.Api.ErrorHandling;

public sealed class ApiEnvelopeProblemDetailsWriter(
    ICorrelationIdAccessor correlationIdAccessor,
    IOptions<ProblemDetailsOptions> problemDetailsOptions,
    IOptions<JsonOptions> jsonOptions) : IProblemDetailsWriter
{
    public const string CorrelationIdExtensionName = "correlationId";

    public bool CanWrite(ProblemDetailsContext context) => true;

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        CompleteProblemDetails(context);
        var envelope = ApiResponse.Failed(context.ProblemDetails);
        var writeTask = context.HttpContext.Response.WriteAsJsonAsync(envelope, jsonOptions.Value.SerializerOptions);
        return new ValueTask(writeTask);
    }

    private void CompleteProblemDetails(ProblemDetailsContext context)
    {
        var problemDetails = context.ProblemDetails;
        problemDetails.Status ??= context.HttpContext.Response.StatusCode;
        problemDetails.Instance ??= ResolveInstance(context.HttpContext);
        problemDetailsOptions.Value.CustomizeProblemDetails?.Invoke(context);
        problemDetails.Extensions[CorrelationIdExtensionName] = correlationIdAccessor.CorrelationId;
    }

    // O UseExceptionHandler limpa o endpoint do contexto; o original fica no IExceptionHandlerFeature.
    private static string ResolveInstance(HttpContext httpContext)
    {
        var endpoint = httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint ?? httpContext.GetEndpoint();
        if (endpoint is RouteEndpoint routeEndpoint && endpoint.Metadata.GetMetadata<SensitiveRouteValuesMetadata>() is not null)
        {
            return routeEndpoint.RoutePattern.RawText ?? string.Empty;
        }

        return httpContext.Request.Path;
    }
}
