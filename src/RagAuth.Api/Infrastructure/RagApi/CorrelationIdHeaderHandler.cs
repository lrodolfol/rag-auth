using RagAuth.Api.Observability;

namespace RagAuth.Api.Infrastructure.RagApi;

public sealed class CorrelationIdHeaderHandler(ICorrelationIdAccessor correlationIdAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = correlationIdAccessor.CorrelationId;
        if (correlationId is not null)
        {
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
