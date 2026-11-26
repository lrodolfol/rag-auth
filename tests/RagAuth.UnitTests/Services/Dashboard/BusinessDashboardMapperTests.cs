using RagAuth.Api.Contracts.Business;
using RagAuth.Api.Contracts.Dashboard;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Services.Dashboard;
using BusinessEntity = RagAuth.Api.Domain.Entities.Business;

namespace RagAuth.UnitTests.Services.Dashboard;

public sealed class BusinessDashboardMapperTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 1, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdatedAt = new(2026, 11, 20, 9, 15, 44, TimeSpan.Zero);

    [Fact]
    public void ClientWithCompleteBusinessMapsEveryField()
    {
        var client = CreateClient(new BusinessEntity
        {
            Name = "Padaria",
            Description = "Pães",
            Email = "contato@padaria.com",
            Telephone = "11987654321",
            Address = "Rua das Flores, 123",
            Details = "Aberto de seg. a sáb.",
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
        });

        var response = BusinessDashboardMapper.ToResponse(client);

        Assert.Equal(new ClientSummaryResponse(42, "Ana", "ana@exemplo.com", true, CreatedAt, UpdatedAt), response.Client);
        Assert.Equal(
            new BusinessResponse("Padaria", "Pães", "contato@padaria.com", "11987654321", "Rua das Flores, 123", "Aberto de seg. a sáb.", CreatedAt, UpdatedAt),
            response.Business);
    }

    [Fact]
    public void BusinessWithoutOptionalFieldsMapsThemAsNull()
    {
        var client = CreateClient(new BusinessEntity { Name = "Padaria", Description = "Pães", CreatedAt = CreatedAt, UpdatedAt = UpdatedAt });

        var business = BusinessDashboardMapper.ToResponse(client).Business!;

        Assert.Equal("Padaria", business.Name);
        Assert.Null(business.Email);
        Assert.Null(business.Telephone);
        Assert.Null(business.Address);
        Assert.Null(business.Details);
    }

    [Fact]
    public void ClientWithoutBusinessMapsBusinessAsNull()
    {
        var response = BusinessDashboardMapper.ToResponse(CreateClient(business: null));

        Assert.Null(response.Business);
        Assert.Equal(42, response.Client.Id);
    }

    private static Client CreateClient(BusinessEntity? business) => new()
    {
        Id = 42,
        AccessCode = "Ab3dEf6hIj9k",
        Name = "Ana",
        Email = "ana@exemplo.com",
        PasswordHash = "hash-secreto",
        EmailConfirmed = true,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        Business = business,
    };
}
