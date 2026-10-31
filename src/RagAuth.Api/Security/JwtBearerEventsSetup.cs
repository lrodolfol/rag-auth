using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using RagAuth.Api.Observability;
using RagAuth.Api.Services.Clients;

namespace RagAuth.Api.Security;

public sealed class JwtBearerEventsSetup(
    IClientSessionValidator clientSessionValidator,
    IProblemDetailsService problemDetailsService,
    ILogger<JwtBearerEventsSetup> logger) : JwtBearerEvents
{
    public const string UnauthenticatedMessage = "Token ausente, inválido ou expirado.";
    public const string ForbiddenMessage = "Você não tem permissão para acessar este recurso.";
    public const string InactiveSessionReason = "a conta do token não existe, foi removida ou o código de acesso não confere";

    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (principal is null || !await clientSessionValidator.IsSessionActiveAsync(principal, context.HttpContext.RequestAborted))
        {
            context.Fail(InactiveSessionReason);
        }
    }

    public override Task Challenge(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        LogMessages.AuthenticationRejected(logger, DescribeFailure(context));
        context.Response.Headers.WWWAuthenticate = JwtBearerDefaults.AuthenticationScheme;
        return WriteProblemDetailsAsync(context.HttpContext, StatusCodes.Status401Unauthorized, UnauthenticatedMessage);
    }

    public override Task Forbidden(ForbiddenContext context)
    {
        var tokenClientId = context.HttpContext.User.FindFirst(ClientClaimTypes.ClientId)?.Value;
        LogMessages.AccessForbidden(logger, tokenClientId, context.Request.Path);
        return WriteProblemDetailsAsync(context.HttpContext, StatusCodes.Status403Forbidden, ForbiddenMessage);
    }

    private static string DescribeFailure(JwtBearerChallengeContext context) => context.AuthenticateFailure switch
    {
        null => "token ausente ou sem o prefixo Bearer",
        { Message: InactiveSessionReason } => InactiveSessionReason,
        var failure => $"token inválido ({failure.GetType().Name})",
    };

    private async Task WriteProblemDetailsAsync(HttpContext httpContext, int statusCode, string detail)
    {
        httpContext.Response.StatusCode = statusCode;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Status = statusCode, Detail = detail },
        });
    }
}
