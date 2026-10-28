using Microsoft.Extensions.Logging;
using Moq;

namespace RagAuth.UnitTests.TestDoubles;

public static class LoggerMocks
{
    public static Mock<ILogger<TCategory>> Create<TCategory>()
    {
        var logger = new Mock<ILogger<TCategory>>();
        logger.Setup(mockedLogger => mockedLogger.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        return logger;
    }

    public static void VerifyLogged<TCategory>(this Mock<ILogger<TCategory>> logger, LogLevel level, Times times) =>
        logger.Verify(
            mockedLogger => mockedLogger.Log(
                level,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);

    public static IReadOnlyList<string> LoggedMessages<TCategory>(this Mock<ILogger<TCategory>> logger) =>
        logger.Invocations
            .Where(invocation => invocation.Method.Name == nameof(ILogger.Log))
            .Select(invocation => invocation.Arguments[2]?.ToString() ?? string.Empty)
            .ToList();
}
