using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.Observability;

namespace RagAuth.Api.ErrorHandling;

public sealed class DomainExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public const string InvalidRequestBodyMessage = "O corpo da requisição é inválido ou está mal formado.";
    public const string UnexpectedErrorMessage = "Ocorreu um erro inesperado. Tente novamente mais tarde.";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problemDetails = CreateProblemDetails(exception);
        httpContext.Response.StatusCode = problemDetails.Status!.Value;
        var problemDetailsContext = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception,
        };
        return await problemDetailsService.TryWriteAsync(problemDetailsContext);
    }

    private ProblemDetails CreateProblemDetails(Exception exception) => exception switch
    {
        DomainException domainException => CreateProblemDetails(domainException.StatusCode, domainException.Title, domainException.Message),
        BadHttpRequestException badRequestException => CreateProblemDetails(
            badRequestException.StatusCode,
            PortugueseProblemDetailsTitles.ForStatus(badRequestException.StatusCode),
            InvalidRequestBodyMessage),
        _ => CreateUnexpectedErrorProblemDetails(exception),
    };

    private ProblemDetails CreateUnexpectedErrorProblemDetails(Exception exception)
    {
        LogMessages.UnexpectedErrorOccurred(logger, exception);
        return CreateProblemDetails(
            StatusCodes.Status500InternalServerError,
            PortugueseProblemDetailsTitles.ForStatus(StatusCodes.Status500InternalServerError),
            UnexpectedErrorMessage);
    }

    private static ProblemDetails CreateProblemDetails(int statusCode, string title, string detail) =>
        new() { Status = statusCode, Title = title, Detail = detail };
}
