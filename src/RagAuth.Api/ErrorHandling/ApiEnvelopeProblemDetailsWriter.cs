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
        problemDetails.Instance ??= context.HttpContext.Request.Path;
        problemDetailsOptions.Value.CustomizeProblemDetails?.Invoke(context);
        problemDetails.Extensions[CorrelationIdExtensionName] = correlationIdAccessor.CorrelationId;
    }
}
