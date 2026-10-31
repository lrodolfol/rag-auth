using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Services.Auth;
using RagAuth.IntegrationTests.Infrastructure;

namespace RagAuth.IntegrationTests.CrossCutting;

[Collection(IntegrationTestCollection.Name)]
public sealed class AccessCodeUniquenessTests(RagAuthApiFactory factory) : IAsyncLifetime
{
    private const int ClientCount = 10_000;

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TenThousandClientsAreStoredWithoutAccessCodeCollision()
    {
        var accessCodeGenerator = factory.Services.GetRequiredService<IAccessCodeGenerator>();

        await factory.ExecuteInDatabaseAsync(async databaseContext =>
        {
            databaseContext.Clients.AddRange(Enumerable.Range(0, ClientCount).Select(index => new Client
            {
                AccessCode = accessCodeGenerator.Generate(),
                Name = $"Cliente {index}",
                Email = $"cliente{index}@exemplo.com",
                PasswordHash = "hash-fixo-para-o-teste",
            }));
            await databaseContext.SaveChangesAsync();

            var distinctAccessCodes = await databaseContext.Clients.Select(client => client.AccessCode).Distinct().CountAsync();
            Assert.Equal(ClientCount, distinctAccessCodes);
        });
    }
}
