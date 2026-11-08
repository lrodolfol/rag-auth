using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Services.EmailConfirmation;
using RagAuth.IntegrationTests.Infrastructure;

namespace RagAuth.IntegrationTests.CrossCutting;

/// <summary>SC-005: nenhum link de confirmação colide em 10.000 emissões.</summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class ConfirmationLinkUniquenessTests(RagAuthApiFactory factory) : IAsyncLifetime
{
    private const int LinkCount = 10_000;

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TenThousandLinksAreStoredWithoutSelectorOrValidatorCollision()
    {
        var linkGenerator = factory.Services.GetRequiredService<IConfirmationLinkGenerator>();
        var validators = Enumerable.Range(0, LinkCount).Select(_ => linkGenerator.GenerateValidator()).ToList();

        await factory.ExecuteInDatabaseAsync(async databaseContext =>
        {
            var client = new Client { AccessCode = "AAAAAAAAAAA1", Name = "Ana", Email = "ana@exemplo.com", PasswordHash = "hash" };
            databaseContext.Clients.Add(client);
            databaseContext.EmailConfirmationLinks.AddRange(validators.Select(validator => CreateUsedLink(client, linkGenerator, validator)));
            await databaseContext.SaveChangesAsync();

            var distinctSelectors = await databaseContext.EmailConfirmationLinks.Select(link => link.Selector).Distinct().CountAsync();
            var distinctHashes = await databaseContext.EmailConfirmationLinks.Select(link => link.ValidatorHash).Distinct().CountAsync();
            Assert.Equal(LinkCount, distinctSelectors);
            Assert.Equal(LinkCount, distinctHashes);
        });
        Assert.Equal(LinkCount, validators.Distinct(StringComparer.Ordinal).Count());
    }

    // Links já usados: o teste mede só a unicidade, sem esbarrar no índice "um link ativo por cliente".
    private static EmailConfirmationLink CreateUsedLink(Client client, IConfirmationLinkGenerator linkGenerator, string validator) => new()
    {
        Client = client,
        Selector = linkGenerator.GenerateSelector(),
        ValidatorHash = linkGenerator.ComputeValidatorHash(validator, client.AccessCode),
        Origin = ConfirmationLinkOrigin.Registration,
        IssuedAt = DateTimeOffset.UtcNow,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
        UsedAt = DateTimeOffset.UtcNow,
    };
}
