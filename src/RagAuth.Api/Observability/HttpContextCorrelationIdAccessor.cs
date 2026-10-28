namespace RagAuth.Api.Observability;

public sealed class HttpContextCorrelationIdAccessor(IHttpContextAccessor httpContextAccessor) : ICorrelationIdAccessor
{
    public string? CorrelationId =>
        httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.HttpContextItemKey] as string;
}
