using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Moq;
using RagAuth.Api.Observability;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Observability;

public sealed class CorrelationIdMiddlewareTests
{
    private readonly Mock<ILogger<CorrelationIdMiddleware>> _logger = LoggerMocks.Create<CorrelationIdMiddleware>();
    private readonly Mock<IDisposable> _logScope = new();

    public CorrelationIdMiddlewareTests()
    {
        _logger.Setup(logger => logger.BeginScope(It.IsAny<It.IsAnyType>())).Returns(_logScope.Object);
    }

    [Fact]
    public async Task StoresCorrelationIdInContextAndResponseHeaderAndCallsNextInsideLogScope()
    {
        var httpContext = new DefaultHttpContext();
        var scopeDisposedBeforeNext = true;
        var middleware = new CorrelationIdMiddleware(_ => RecordScopeState(), _logger.Object);

        await middleware.InvokeAsync(httpContext);

        var correlationId = Assert.IsType<string>(httpContext.Items[CorrelationIdMiddleware.HttpContextItemKey]);
        Assert.True(Guid.TryParse(correlationId, out _));
        Assert.Equal(correlationId, httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
        Assert.False(scopeDisposedBeforeNext);
        _logger.Verify(logger => logger.BeginScope(It.Is<It.IsAnyType>((scope, _) => scope != null &&
            Equals(scope.ToString(), $"CorrelationId: {correlationId}"))));
        _logScope.Verify(scope => scope.Dispose(), Times.Once());

        Task RecordScopeState()
        {
            scopeDisposedBeforeNext = _logScope.Invocations.Count > 0;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task GeneratesNewCorrelationIdForEachRequest()
    {
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, _logger.Object);
        var firstContext = new DefaultHttpContext();
        var secondContext = new DefaultHttpContext();

        await middleware.InvokeAsync(firstContext);
        await middleware.InvokeAsync(secondContext);

        Assert.NotEqual(
            firstContext.Items[CorrelationIdMiddleware.HttpContextItemKey],
            secondContext.Items[CorrelationIdMiddleware.HttpContextItemKey]);
    }

    [Fact]
    public async Task RestoresHeaderWhenResponseStartsAfterHeadersWereCleared()
    {
        var responseFeature = new CallbackCapturingResponseFeature();
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<IHttpResponseFeature>(responseFeature);
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, _logger.Object);

        await middleware.InvokeAsync(httpContext);
        httpContext.Response.Headers.Clear();
        await responseFeature.StartAsync();

        Assert.Equal(
            httpContext.Items[CorrelationIdMiddleware.HttpContextItemKey],
            httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public void AccessorReadsCorrelationIdFromCurrentHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationIdMiddleware.HttpContextItemKey] = "id-de-teste";
        var accessor = new HttpContextCorrelationIdAccessor(new HttpContextAccessor { HttpContext = httpContext });

        Assert.Equal("id-de-teste", accessor.CorrelationId);
    }

    private sealed class CallbackCapturingResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _startingCallbacks = [];

        public override void OnStarting(Func<object, Task> callback, object state) => _startingCallbacks.Add((callback, state));

        public async Task StartAsync()
        {
            foreach (var (callback, state) in _startingCallbacks)
            {
                await callback(state);
            }
        }
    }
}
