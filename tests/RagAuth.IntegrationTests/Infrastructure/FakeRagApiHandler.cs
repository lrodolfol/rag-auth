using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace RagAuth.IntegrationTests.Infrastructure;

public sealed record RecordedRagRequest(
    HttpMethod Method,
    string Path,
    IReadOnlyDictionary<string, string> Headers,
    string? Body);

public sealed class FakeRagApiHandler : HttpMessageHandler
{
    public const string InternalResponseBody = "{\"detalheInterno\":\"stacktrace-secreto-da-rag\"}";

    private readonly ConcurrentQueue<RecordedRagRequest> _requests = new();
    private Func<CancellationToken, Task<HttpResponseMessage>> _respond = RespondWithStatus(HttpStatusCode.OK);

    public IReadOnlyList<RecordedRagRequest> Requests => _requests.ToArray();

    public void RespondWith(HttpStatusCode statusCode) => _respond = RespondWithStatus(statusCode);

    public void FailWithConnectionError() =>
        _respond = _ => Task.FromException<HttpResponseMessage>(new HttpRequestException("Conexão recusada."));

    public void DelayResponse(TimeSpan delay) => _respond = async cancellationToken =>
    {
        await Task.Delay(delay, cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK);
    };

    public void Reset()
    {
        _requests.Clear();
        _respond = RespondWithStatus(HttpStatusCode.OK);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Enqueue(await RecordAsync(request, cancellationToken));
        return await _respond(cancellationToken);
    }

    private static Func<CancellationToken, Task<HttpResponseMessage>> RespondWithStatus(HttpStatusCode statusCode) =>
        _ => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(InternalResponseBody, Encoding.UTF8, "application/json"),
        });

    private static async Task<RecordedRagRequest> RecordAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var headers = request.Headers
            .Concat(request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
            .ToDictionary(header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase);
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return new RecordedRagRequest(request.Method, request.RequestUri!.AbsolutePath, headers, body);
    }
}
