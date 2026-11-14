using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Infrastructure.Persistence;
using RagAuth.Api.Services.PasswordRecovery;
using RagAuth.IntegrationTests.Infrastructure;

namespace RagAuth.IntegrationTests.CrossCutting;

/// <summary>SC-005: nenhum link de recuperação colide em 10.000 emissões.</summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class PasswordRecoveryLinkUniquenessTests(RagAuthApiFactory factory) : IAsyncLifetime
{
    private const int LinkCount = 10_000;

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TenThousandRecoveryLinksHaveNoSelectorOrUrlCollision()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var databaseContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var linkIssuer = scope.ServiceProvider.GetRequiredService<IPasswordRecoveryLinkIssuer>();
        var client = new Client { AccessCode = "AAAAAAAAAAA1", Name = "Ana", Email = "ana@exemplo.com", PasswordHash = "hash", EmailConfirmed = true };
        databaseContext.Clients.Add(client);

        var urls = Enumerable.Range(0, LinkCount).Select(_ => IssueUsedLink(linkIssuer, client)).ToList();
        await databaseContext.SaveChangesAsync();

        Assert.Equal(LinkCount, urls.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(LinkCount, await databaseContext.PasswordRecoveryLinks.Select(link => link.Selector).Distinct().CountAsync());
        Assert.Equal(LinkCount, await databaseContext.PasswordRecoveryLinks.Select(link => link.ValidatorHash).Distinct().CountAsync());
    }

    // Links marcados como usados: o teste mede só a unicidade, sem esbarrar no índice "um link ativo por cliente".
    private static string IssueUsedLink(IPasswordRecoveryLinkIssuer linkIssuer, Client client)
    {
        var issuedLink = linkIssuer.Issue(client);
        issuedLink.Entity.UsedAt = issuedLink.Entity.IssuedAt;
        return issuedLink.Message.ActionUrl!;
    }
}
