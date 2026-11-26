using RagAuth.Api.Contracts.Dashboard;
using RagAuth.Api.Domain.Entities;
using RagAuth.Api.Services.Business;

namespace RagAuth.Api.Services.Dashboard;

public static class BusinessDashboardMapper
{
    public static ClientSummaryResponse ToClientSummary(Client client) =>
        new(
            client.Id,
            client.Name,
            client.Email,
            client.EmailConfirmed,
            client.CreatedAt,
            client.UpdatedAt);

    public static BusinessDashboardResponse ToResponse(Client client) =>
        new(
            ToClientSummary(client),
            client.Business is null ? null : BusinessMapper.ToResponse(client.Business));
}
