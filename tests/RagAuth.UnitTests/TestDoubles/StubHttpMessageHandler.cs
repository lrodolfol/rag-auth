using System.Net;

namespace RagAuth.UnitTests.TestDoubles;

public sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastRequestBody { get; private set; }

    public long? LastRequestContentLength { get; private set; }

    public static StubHttpMessageHandler RespondingWith(HttpStatusCode statusCode) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)));

    public static StubHttpMessageHandler Throwing(Exception exception) =>
        new((_, _) => Task.FromException<HttpResponseMessage>(exception));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastRequestContentLength = request.Content?.Headers.ContentLength;
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return await respond(request, cancellationToken);
    }
}
