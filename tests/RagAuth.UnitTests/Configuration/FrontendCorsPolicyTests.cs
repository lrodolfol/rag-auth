using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RagAuth.Api.Configuration;
using RagAuth.Api.Security;

namespace RagAuth.UnitTests.Configuration;

public sealed class FrontendCorsPolicyTests
{
    [Fact]
    public async Task FrontendPolicyAllowsOnlyConfiguredOriginMethodsAndAnyHeader()
    {
        var policy = await ResolveFrontendPolicyAsync("http://localhost:4200/");

        Assert.Equal(["http://localhost:4200"], policy.Origins);
        Assert.Equal(["GET", "POST", "PUT", "DELETE"], policy.Methods);
        Assert.True(policy.AllowAnyHeader);
        Assert.False(policy.AllowAnyOrigin);
        Assert.False(policy.AllowAnyMethod);
        Assert.False(policy.SupportsCredentials);
    }

    [Fact]
    public async Task FrontendPolicyKeepsEveryConfiguredOrigin()
    {
        var policy = await ResolveFrontendPolicyAsync(" http://localhost:4200 ", "https://front.ragauth.com.br/");

        Assert.Equal(["http://localhost:4200", "https://front.ragauth.com.br"], policy.Origins);
    }

    private static async Task<CorsPolicy> ResolveFrontendPolicyAsync(params string[] allowedOrigins)
    {
        var services = new ServiceCollection();
        services.AddFrontendCors();
        services.Configure<FrontendCorsOptions>(options => options.AllowedOrigins = allowedOrigins);
        await using var serviceProvider = services.BuildServiceProvider();
        var policyProvider = serviceProvider.GetRequiredService<ICorsPolicyProvider>();
        var policy = await policyProvider.GetPolicyAsync(new DefaultHttpContext(), CorsServiceExtensions.FrontendPolicyName);
        return Assert.IsType<CorsPolicy>(policy);
    }
}
