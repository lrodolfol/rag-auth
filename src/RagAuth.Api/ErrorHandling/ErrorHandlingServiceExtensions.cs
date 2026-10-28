using RagAuth.Api.Domain.Exceptions;

namespace RagAuth.Api.ErrorHandling;

public static class ErrorHandlingServiceExtensions
{
    public static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        services.Insert(0, ServiceDescriptor.Singleton<IProblemDetailsWriter, ApiEnvelopeProblemDetailsWriter>());
        services.AddProblemDetails(options => options.CustomizeProblemDetails = CustomizeProblemDetails);
        services.AddExceptionHandler<DomainExceptionHandler>();
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        return services;
    }

    private static void CustomizeProblemDetails(ProblemDetailsContext context)
    {
        TranslateFrameworkTitle(context);
        if (context.ProblemDetails is HttpValidationProblemDetails validationProblemDetails)
        {
            ValidationErrorsFormatter.FormatErrors(validationProblemDetails);
        }
    }

    private static void TranslateFrameworkTitle(ProblemDetailsContext context)
    {
        if (context.Exception is DomainException)
        {
            return;
        }

        var statusCode = context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode;
        context.ProblemDetails.Title = PortugueseProblemDetailsTitles.ForStatus(statusCode);
    }
}
