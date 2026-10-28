using Microsoft.Extensions.Options;
using RagAuth.Api.Configuration;

namespace RagAuth.Api.Infrastructure.RagApi;

public sealed class ApiKeyHeaderHandler(IOptions<RagApiOptions> ragApiOptions) : DelegatingHandler
{
    public const string HeaderName = "x-api-key";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove(HeaderName);
        request.Headers.Add(HeaderName, ragApiOptions.Value.ApiKey);
        return base.SendAsync(request, cancellationToken);
    }
}
