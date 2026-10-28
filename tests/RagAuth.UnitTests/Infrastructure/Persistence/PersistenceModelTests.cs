using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Infrastructure.Persistence;
using BusinessEntity = RagAuth.Api.Domain.Entities.Business;

namespace RagAuth.UnitTests.Infrastructure.Persistence;

public sealed class PersistenceModelTests
{
    private static readonly DateTimeOffset CreationTime = new(2026, 10, 25, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ModelUsesCamelCaseColumnsAndNamedUniqueIndexes()
    {
        using var databaseContext = CreateDatabaseContext();
        var clientType = databaseContext.Model.FindEntityType(typeof(Client))!;
        var businessType = databaseContext.Model.FindEntityType(typeof(BusinessEntity))!;

        Assert.Equal("clients", clientType.GetTableName());
        Assert.Equal("accessCode", clientType.FindProperty(nameof(Client.AccessCode))!.GetColumnName());
        Assert.Equal("clientId", businessType.FindProperty(nameof(BusinessEntity.ClientId))!.GetColumnName());
        var emailIndex = clientType.GetIndexes().Single(index => index.GetDatabaseName() == UniqueIndexNames.ActiveClientEmail);
        Assert.True(emailIndex.IsUnique);
        Assert.Equal("\"deletedAt\" IS NULL", emailIndex.GetFilter());
        Assert.Contains(clientType.GetIndexes(), index => index.GetDatabaseName() == UniqueIndexNames.ClientAccessCode && index.IsUnique);
        Assert.Contains(businessType.GetIndexes(), index => index.GetDatabaseName() == UniqueIndexNames.BusinessClientId && index.IsUnique);
    }

    [Fact]
    public void InterceptorFillsBothTimestampsOnInsert()
    {
        using var databaseContext = CreateDatabaseContext();
        var client = new Client();
        databaseContext.Clients.Add(client);

        CreateInterceptor(CreationTime).SavingChanges(CreateEventData(databaseContext), default);

        Assert.Equal(CreationTime, client.CreatedAt);
        Assert.Equal(CreationTime, client.UpdatedAt);
    }

    [Fact]
    public async Task InterceptorUpdatesOnlyUpdatedAtOnChange()
    {
        using var databaseContext = CreateDatabaseContext();
        var business = new BusinessEntity { Id = 1, CreatedAt = CreationTime, UpdatedAt = CreationTime };
        databaseContext.Businesses.Attach(business);
        databaseContext.Entry(business).State = EntityState.Modified;
        var changeTime = CreationTime.AddHours(1);

        await CreateInterceptor(changeTime).SavingChangesAsync(CreateEventData(databaseContext), default);

        Assert.Equal(CreationTime, business.CreatedAt);
        Assert.Equal(changeTime, business.UpdatedAt);
    }

    [Fact]
    public void InterceptorIgnoresMissingContext()
    {
        var result = CreateInterceptor(CreationTime).SavingChanges(new DbContextEventData(null!, null!, null), default);

        Assert.False(result.HasResult);
    }

    private static AppDbContext CreateDatabaseContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=modelo-sem-conexao").Options);

    private static DbContextEventData CreateEventData(DbContext databaseContext) => new(null!, null!, databaseContext);

    private static TimestampInterceptor CreateInterceptor(DateTimeOffset currentTime)
    {
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(provider => provider.GetUtcNow()).Returns(currentTime);
        return new TimestampInterceptor(timeProvider.Object);
    }
}
