using RagAuth.Api.Contracts;
using RagAuth.Api.Contracts.PasswordRecovery;
using RagAuth.Api.ErrorHandling;
using RagAuth.Api.Services.PasswordRecovery;

namespace RagAuth.Api.Endpoints;

public static class PasswordRecoveryEndpoints
{
    public const string RecoveryRequestAcceptedMessage = "Se o e-mail estiver cadastrado, enviaremos um link para redefinir a senha em instantes.";
    public const string ValidLinkMessage = "Link válido. Informe a nova senha.";
    public const string PasswordResetMessage = "Senha alterada com sucesso. Você já pode fazer login com a nova senha.";

    private const string RecoveryLinkRoute = "/{selector}/" + PasswordRecoveryLinkIssuer.RecoveryPathSegment + "/{validator}";
    private const string RecoveryRequestRoute = "/new-" + PasswordRecoveryLinkIssuer.RecoveryPathSegment + "-request";

    public static IEndpointRouteBuilder MapPasswordRecoveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var clientGroup = endpoints.MapGroup("/api/v1/client").AllowAnonymous();
        clientGroup.MapPost(RecoveryRequestRoute, RequestPasswordRecoveryAsync);
        clientGroup.MapGet(RecoveryLinkRoute, VerifyPasswordRecoveryLinkAsync)
            .WithMetadata(new SensitiveRouteValuesMetadata());
        clientGroup.MapPut(RecoveryLinkRoute, ResetPasswordAsync)
            .WithMetadata(new SensitiveRouteValuesMetadata());
        return endpoints;
    }

    internal static async Task<IResult> RequestPasswordRecoveryAsync(
        PasswordRecoveryRequest request,
        IPasswordRecoveryService passwordRecoveryService,
        CancellationToken cancellationToken)
    {
        await passwordRecoveryService.RequestRecoveryAsync(request.Email, cancellationToken);
        var response = ApiResponse.Succeeded<object?>(StatusCodes.Status202Accepted, RecoveryRequestAcceptedMessage, null);
        return TypedResults.Json(response, statusCode: StatusCodes.Status202Accepted);
    }

    internal static async Task<IResult> VerifyPasswordRecoveryLinkAsync(
        string selector,
        string validator,
        IPasswordRecoveryService passwordRecoveryService,
        CancellationToken cancellationToken)
    {
        await passwordRecoveryService.VerifyLinkAsync(selector, validator, cancellationToken);
        var status = new PasswordRecoveryLinkStatusResponse(PasswordRecoveryLinkStatusResponse.ValidStatus);
        return TypedResults.Json(ApiResponse.Succeeded(StatusCodes.Status200OK, ValidLinkMessage, status), statusCode: StatusCodes.Status200OK);
    }

    internal static async Task<IResult> ResetPasswordAsync(
        string selector,
        string validator,
        ResetPasswordRequest request,
        IPasswordRecoveryService passwordRecoveryService,
        CancellationToken cancellationToken)
    {
        await passwordRecoveryService.ResetPasswordAsync(selector, validator, request.NewPassword, cancellationToken);
        var response = ApiResponse.Succeeded<object?>(StatusCodes.Status200OK, PasswordResetMessage, null);
        return TypedResults.Json(response, statusCode: StatusCodes.Status200OK);
    }
}
