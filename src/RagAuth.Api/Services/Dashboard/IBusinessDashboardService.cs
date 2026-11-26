namespace RagAuth.Api.Services.Dashboard;

public interface IBusinessDashboardService
{
    Task<BusinessDashboardResult> GetDashboardAsync(int clientId, CancellationToken cancellationToken = default);
}
