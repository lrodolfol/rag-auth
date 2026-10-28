using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using RagAuth.Api.Security;

namespace RagAuth.UnitTests.Security;

public sealed class ClientRouteMatchesTokenHandlerTests
{
    [Fact]
    public async Task SucceedsWhenRouteClientIdEqualsTokenClaim()
    {
        Assert.True(await AuthorizeAsync(routeClientId: "42", tokenClientId: "42"));
    }

    [Theory]
    [InlineData("7", "42")]
    [InlineData("abc", "42")]
    [InlineData(null, "42")]
    [InlineData("42", null)]
    public async Task FailsWhenRouteClientIdDiffersIsInvalidOrMissing(string? routeClientId, string? tokenClientId)
    {
        Assert.False(await AuthorizeAsync(routeClientId, tokenClientId));
    }

    [Fact]
    public async Task FailsWhenResourceIsNotHttpContext()
    {
        var requirement = new ClientRouteMatchesTokenRequirement();
        var context = new AuthorizationHandlerContext([requirement], CreateUser("42"), resource: null);

        await new ClientRouteMatchesTokenHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private static async Task<bool> AuthorizeAsync(string? routeClientId, string? tokenClientId)
    {
        var httpContext = new DefaultHttpContext();
        if (routeClientId is not null)
        {
            httpContext.Request.RouteValues["clientId"] = routeClientId;
        }

        var requirement = new ClientRouteMatchesTokenRequirement();
        var context = new AuthorizationHandlerContext([requirement], CreateUser(tokenClientId), httpContext);
        await new ClientRouteMatchesTokenHandler().HandleAsync(context);
        return context.HasSucceeded;
    }

    private static ClaimsPrincipal CreateUser(string? tokenClientId)
    {
        var claims = tokenClientId is null ? [] : new[] { new Claim("client_id", tokenClientId) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }
}
