using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
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
    [InlineData("clients", new[] { "id", "accessCode", "name", "email", "passwordHash", "createdAt", "updatedAt", "deletedAt", "emailConfirmed", "emailConfirmedAt", "passwordChangedAt" })]
    [InlineData("email_confirmation_links", new[] { "id", "clientId", "selector", "validatorHash", "purpose", "origin", "issuedAt", "expiresAt", "usedAt", "revokedAt" })]
    [InlineData("password_recovery_links", new[] { "id", "clientId", "selector", "validatorHash", "purpose", "issuedAt", "expiresAt", "usedAt", "revokedAt" })]
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

    [Fact]
    public async Task EmailConfirmedDefaultsToFalseAndConfirmationIndexesExist()
    {
        await factory.ExecuteInDatabaseAsync(async databaseContext =>
        {
            var columnDefault = await databaseContext.Database
                .SqlQuery<string>($"SELECT column_default AS \"Value\" FROM information_schema.columns WHERE table_name = 'clients' AND column_name = 'emailConfirmed'")
                .SingleAsync();
            var indexDefinitions = await databaseContext.Database
                .SqlQuery<string>($"SELECT indexname || ' ' || indexdef AS \"Value\" FROM pg_indexes WHERE tablename = 'email_confirmation_links'")
                .ToListAsync();

            Assert.Equal("false", columnDefault);
            Assert.Contains(indexDefinitions, index => index.StartsWith(UniqueIndexNames.EmailConfirmationLinkSelector, StringComparison.Ordinal) && index.Contains("UNIQUE"));
            Assert.Contains(indexDefinitions, index => index.StartsWith(UniqueIndexNames.ActiveEmailConfirmationLinkPerClient, StringComparison.Ordinal)
                && index.Contains("UNIQUE") && index.Contains("\"usedAt\" IS NULL") && index.Contains("\"revokedAt\" IS NULL"));
            Assert.Contains(indexDefinitions, index => index.StartsWith("ix_email_confirmation_links_client_issued", StringComparison.Ordinal));
        });
    }

    [Fact]
    public async Task PasswordRecoveryIndexesAndNullablePasswordChangedAtExist()
    {
        await factory.ExecuteInDatabaseAsync(async databaseContext =>
        {
            var passwordChangedAtNullable = await databaseContext.Database
                .SqlQuery<string>($"SELECT is_nullable AS \"Value\" FROM information_schema.columns WHERE table_name = 'clients' AND column_name = 'passwordChangedAt'")
                .SingleAsync();
            var indexDefinitions = await databaseContext.Database
                .SqlQuery<string>($"SELECT indexname || ' ' || indexdef AS \"Value\" FROM pg_indexes WHERE tablename = 'password_recovery_links'")
                .ToListAsync();

            Assert.Equal("YES", passwordChangedAtNullable);
            Assert.Contains(indexDefinitions, index => index.StartsWith(UniqueIndexNames.PasswordRecoveryLinkSelector, StringComparison.Ordinal) && index.Contains("UNIQUE"));
            Assert.Contains(indexDefinitions, index => index.StartsWith(UniqueIndexNames.ActivePasswordRecoveryLinkPerClient, StringComparison.Ordinal)
                && index.Contains("UNIQUE") && index.Contains("\"usedAt\" IS NULL") && index.Contains("\"revokedAt\" IS NULL"));
            Assert.Contains(indexDefinitions, index => index.StartsWith("ix_password_recovery_links_client_issued", StringComparison.Ordinal));
        });
    }

    [Fact]
    public async Task OnlyOneActiveConfirmationLinkPerClientIsAllowed()
    {
        await factory.ExecuteInDatabaseAsync(async databaseContext =>
        {
            var client = CreateClient("ana@exemplo.com", "AAAAAAAAAAA1");
            databaseContext.Clients.Add(client);
            databaseContext.EmailConfirmationLinks.Add(CreateLink(client, "selector-0000000000001"));
            await databaseContext.SaveChangesAsync();

            databaseContext.EmailConfirmationLinks.Add(CreateLink(client, "selector-0000000000002"));
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => databaseContext.SaveChangesAsync());

            Assert.True(UniqueConstraintViolation.IsViolationOf(exception, UniqueIndexNames.ActiveEmailConfirmationLinkPerClient));
        });
    }

    [Fact]
    public async Task MigrationMarksExistingNonRemovedAccountsAsConfirmed()
    {
        const string PreviousMigration = "20261028231002_v1";
        await factory.ExecuteInDatabaseAsync(async databaseContext =>
        {
            var migrator = databaseContext.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            await databaseContext.Database.ExecuteSqlRawAsync("""
                INSERT INTO clients ("accessCode", name, email, "passwordHash", "createdAt", "updatedAt", "deletedAt") VALUES
                ('AAAAAAAAAAA1', 'Ana', 'ana@exemplo.com', 'hash', '2026-01-02T03:04:05Z', now(), NULL),
                ('AAAAAAAAAAA2', 'Bia', 'bia@exemplo.com', 'hash', now(), now(), now())
                """);
            await migrator.MigrateAsync();

            var clients = await databaseContext.Clients.IgnoreQueryFilters().AsNoTracking().OrderBy(client => client.AccessCode).ToListAsync();
            Assert.True(clients[0].EmailConfirmed);
            Assert.Equal(clients[0].CreatedAt, clients[0].EmailConfirmedAt);
            Assert.False(clients[1].EmailConfirmed);
            Assert.Null(clients[1].EmailConfirmedAt);
        });
    }

    private static EmailConfirmationLink CreateLink(Client client, string selector) => new()
    {
        Client = client,
        Selector = selector,
        ValidatorHash = new string('a', 64),
        Origin = ConfirmationLinkOrigin.Registration,
        IssuedAt = DateTimeOffset.UtcNow,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
    };

    private static Client CreateClient(string email, string accessCode) => new()
    {
        AccessCode = accessCode,
        Name = "Ana",
        Email = email,
        PasswordHash = "hash",
    };
}
