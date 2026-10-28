namespace RagAuth.Api.Observability;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string HttpContextItemKey = "CorrelationId";
    public const string LogScopeTemplate = "CorrelationId: {CorrelationId}";

    public async Task InvokeAsync(HttpContext httpContext)
    {
        var correlationId = Guid.NewGuid().ToString();
        httpContext.Items[HttpContextItemKey] = correlationId;
        httpContext.Response.Headers[HeaderName] = correlationId;
        httpContext.Response.OnStarting(() => RestoreHeader(httpContext, correlationId));
        using (logger.BeginScope(LogScopeTemplate, correlationId))
        {
            await next(httpContext);
        }
    }

    // O UseExceptionHandler limpa os cabeçalhos da resposta antes de escrever o erro.
    private static Task RestoreHeader(HttpContext httpContext, string correlationId)
    {
        httpContext.Response.Headers[HeaderName] = correlationId;
        return Task.CompletedTask;
    }
}
