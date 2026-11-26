using RagAuth.Api.Contracts.Dashboard;

namespace RagAuth.Api.Services.Dashboard;

public sealed record BusinessDashboardResult(string Message, BusinessDashboardResponse Dashboard);
