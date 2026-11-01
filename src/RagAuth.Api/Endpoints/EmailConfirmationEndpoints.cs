using RagAuth.Api.Contracts;
using RagAuth.Api.Contracts.EmailConfirmation;
using RagAuth.Api.ErrorHandling;
using RagAuth.Api.Services.EmailConfirmation;

namespace RagAuth.Api.Endpoints;

public static class EmailConfirmationEndpoints
{
    public const string ConfirmedMessage = "E-mail confirmado com sucesso. Sua conta está ativa e você já pode fazer login.";
    public const string AlreadyConfirmedMessage = "Este e-mail já foi confirmado. Você já pode fazer login.";
    public const string NewLinkAcceptedMessage = "Se o e-mail estiver cadastrado e aguardando confirmação, enviaremos um novo link em instantes.";

    public static IEndpointRouteBuilder MapEmailConfirmationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var clientGroup = endpoints.MapGroup("/api/v1/client").AllowAnonymous();
        clientGroup.MapGet("/{clientIdentificationHash}/confirmation-register/{keyHash}", ConfirmEmailAsync)
            .WithMetadata(new SensitiveRouteValuesMetadata());
        clientGroup.MapPost("/new-confirmation-register", RequestNewConfirmationLinkAsync);
        return endpoints;
    }

    internal static async Task<IResult> ConfirmEmailAsync(
        string clientIdentificationHash,
        string keyHash,
        IEmailConfirmationService emailConfirmationService,
        CancellationToken cancellationToken)
    {
        var outcome = await emailConfirmationService.ConfirmAsync(clientIdentificationHash, keyHash, cancellationToken);
        var response = outcome == EmailConfirmationOutcome.Confirmed
            ? ApiResponse.Succeeded(StatusCodes.Status200OK, ConfirmedMessage, new EmailConfirmationResultResponse(EmailConfirmationResultResponse.ConfirmedStatus))
            : ApiResponse.Succeeded(StatusCodes.Status200OK, AlreadyConfirmedMessage, new EmailConfirmationResultResponse(EmailConfirmationResultResponse.AlreadyConfirmedStatus));
        return TypedResults.Json(response, statusCode: StatusCodes.Status200OK);
    }

    internal static async Task<IResult> RequestNewConfirmationLinkAsync(
        NewConfirmationLinkRequest request,
        IEmailConfirmationService emailConfirmationService,
        CancellationToken cancellationToken)
    {
        await emailConfirmationService.RequestNewLinkAsync(request.Email, cancellationToken);
        var response = ApiResponse.Succeeded<object?>(StatusCodes.Status202Accepted, NewLinkAcceptedMessage, null);
        return TypedResults.Json(response, statusCode: StatusCodes.Status202Accepted);
    }
}
