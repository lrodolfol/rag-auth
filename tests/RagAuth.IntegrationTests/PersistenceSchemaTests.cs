using Microsoft.EntityFrameworkCore;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Infrastructure.Persistence;
using RagAuth.IntegrationTests.Infrastructure;

namespace RagAuth.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class PersistenceSchemaTests(RagAuthApiFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("clients", new[] { "id", "accessCode", "name", "email", "passwordHash", "createdAt", "updatedAt", "deletedAt" })]
    [InlineData("businesses", new[] { "id", "clientId", "name", "description", "email", "telephone", "address", "details", "createdAt", "updatedAt" })]
    public async Task MigrationCreatesTablesWithCamelCaseColumns(string tableName, string[] expectedColumns)
    {
        await factory.ExecuteInDatabaseAsync(async databaseContext =>
        {
            var columns = await databaseContext.Database
                .SqlQuery<string>($"SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_name = {tableName}")
                .ToListAsync();

            Assert.Equal(expectedColumns.Order(), columns.Order());
        });
    }

    [Fact]
    public async Task TimestampInterceptorFillsCreatedAtOnInsertAndOnlyUpdatedAtOnChange()
    {
        await factory.ExecuteInDatabaseAsync(async databaseContext =>
        {
            var client = CreateClient("ana@exemplo.com", "AAAAAAAAAAA1");
            databaseContext.Clients.Add(client);
            await databaseContext.SaveChangesAsync();
            var createdAt = client.CreatedAt;
            Assert.Equal(createdAt, client.UpdatedAt);

            await Task.Delay(20);
            client.Name = "Ana Maria";
            await databaseContext.SaveChangesAsync();

            Assert.Equal(createdAt, client.CreatedAt);
            Assert.True(client.UpdatedAt > createdAt);
        });
    }

    [Fact]
    public async Task ActiveEmailMustBeUniqueButRemovedAccountsDoNotBlockIt()
    {
        await factory.ExecuteInDatabaseAsync(async databaseContext =>
        {
            var removedClient = CreateClient("ana@exemplo.com", "AAAAAAAAAAA1");
            removedClient.DeletedAt = DateTimeOffset.UtcNow;
            databaseContext.Clients.AddRange(removedClient, CreateClient("ana@exemplo.com", "AAAAAAAAAAA2"));
            await databaseContext.SaveChangesAsync();

            databaseContext.Clients.Add(CreateClient("ana@exemplo.com", "AAAAAAAAAAA3"));
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => databaseContext.SaveChangesAsync());

            Assert.True(UniqueConstraintViolation.IsViolationOf(exception, UniqueIndexNames.ActiveClientEmail));
        });
    }

    private static Client CreateClient(string email, string accessCode) => new()
    {
        AccessCode = accessCode,
        Name = "Ana",
        Email = email,
        PasswordHash = "hash",
    };
}
