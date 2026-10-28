using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.Observability;

namespace RagAuth.Api.Infrastructure.RagApi;

public sealed class RagApiResponseTranslator(ILogger<RagApiResponseTranslator> logger) : IRagApiResponseTranslator
{
    public DomainException Translate(string operation, HttpResponseMessage unsuccessfulResponse)
    {
        var statusCode = (int)unsuccessfulResponse.StatusCode;
        return statusCode switch
        {
            StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden => TranslateRejectedApiKey(operation, statusCode),
            >= 400 and < 500 => TranslateRejectedRequest(operation, statusCode),
            _ => TranslateIntegrationFailure(operation, statusCode),
        };
    }

    private RagApiIntegrationException TranslateRejectedApiKey(string operation, int statusCode)
    {
        LogMessages.RagApiKeyRejected(logger, operation, statusCode);
        return new RagApiIntegrationException();
    }

    private RagApiRejectedException TranslateRejectedRequest(string operation, int statusCode)
    {
        LogMessages.RagApiRejectedRequest(logger, operation, statusCode);
        return new RagApiRejectedException(statusCode);
    }

    private RagApiIntegrationException TranslateIntegrationFailure(string operation, int statusCode)
    {
        LogMessages.RagApiFailed(logger, operation, $"status {statusCode}");
        return new RagApiIntegrationException();
    }
}
