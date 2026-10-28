using Microsoft.Net.Http.Headers;

namespace RagAuth.Api.Infrastructure.RagApi;

public sealed class ClientTokenForwardingHandler(ICurrentRequestTokenAccessor requestTokenAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authorizationHeader = requestTokenAccessor.AuthorizationHeader;
        if (authorizationHeader is not null)
        {
            request.Headers.TryAddWithoutValidation(HeaderNames.Authorization, authorizationHeader);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
