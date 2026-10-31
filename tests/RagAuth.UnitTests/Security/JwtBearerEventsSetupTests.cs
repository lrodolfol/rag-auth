using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using RagAuth.Api.Security;
using RagAuth.Api.Services.Clients;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Security;

public sealed class JwtBearerEventsSetupTests
{
    private static readonly AuthenticationScheme BearerScheme = new(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));

    private readonly Mock<IClientSessionValidator> _sessionValidator = new();
    private readonly Mock<IProblemDetailsService> _problemDetailsService = new();
    private readonly Mock<ILogger<JwtBearerEventsSetup>> _logger = LoggerMocks.Create<JwtBearerEventsSetup>();
    private ProblemDetailsContext? _writtenProblem;

    public JwtBearerEventsSetupTests()
    {
        _problemDetailsService
            .Setup(service => service.WriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(context => _writtenProblem = context)
            .Returns(ValueTask.CompletedTask);
    }

    [Fact]
    public async Task TokenOfActiveSessionIsKept()
    {
        var context = CreateTokenValidatedContext(sessionIsActive: true);

        await CreateEvents().TokenValidated(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task TokenOfInactiveSessionFailsAuthentication()
    {
        var context = CreateTokenValidatedContext(sessionIsActive: false);

        await CreateEvents().TokenValidated(context);

        Assert.Equal(JwtBearerEventsSetup.InactiveSessionReason, context.Result!.Failure!.Message);
    }

    [Theory]
    [InlineData(null, "token ausente ou sem o prefixo Bearer")]
    [InlineData("inativa", JwtBearerEventsSetup.InactiveSessionReason)]
    [InlineData("expirado", "token inválido (SecurityTokenExpiredException)")]
    public async Task ChallengeWritesUnauthorizedProblemAndLogsReasonWithoutToken(string? failureKind, string expectedReason)
    {
        var httpContext = new DefaultHttpContext();
        var context = new JwtBearerChallengeContext(httpContext, BearerScheme, new JwtBearerOptions(), new AuthenticationProperties())
        {
            AuthenticateFailure = failureKind switch
            {
                "inativa" => new AuthenticationFailureException(JwtBearerEventsSetup.InactiveSessionReason),
                "expirado" => new SecurityTokenExpiredException("IDX10223"),
                _ => null,
            },
        };

        await CreateEvents().Challenge(context);

        Assert.True(context.Handled);
        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.Equal("Bearer", httpContext.Response.Headers.WWWAuthenticate.ToString());
        Assert.Equal(JwtBearerEventsSetup.UnauthenticatedMessage, _writtenProblem!.ProblemDetails.Detail);
        Assert.Contains(_logger.LoggedMessages(), message => message.Contains(expectedReason));
    }

    [Fact]
    public async Task ForbiddenWritesForbiddenProblemAndLogsClientAndPath()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/7/business";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("client_id", "42")], "Bearer"));

        await CreateEvents().Forbidden(new ForbiddenContext(httpContext, BearerScheme, new JwtBearerOptions()));

        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Equal(JwtBearerEventsSetup.ForbiddenMessage, _writtenProblem!.ProblemDetails.Detail);
        Assert.Contains(_logger.LoggedMessages(), message => message.Contains("42") && message.Contains("/api/v1/7/business"));
    }

    private TokenValidatedContext CreateTokenValidatedContext(bool sessionIsActive)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("client_id", "42")], "Bearer"));
        _sessionValidator.Setup(validator => validator.IsSessionActiveAsync(principal, It.IsAny<CancellationToken>())).ReturnsAsync(sessionIsActive);
        return new TokenValidatedContext(new DefaultHttpContext(), BearerScheme, new JwtBearerOptions()) { Principal = principal };
    }

    private JwtBearerEventsSetup CreateEvents() => new(_sessionValidator.Object, _problemDetailsService.Object, _logger.Object);
}
