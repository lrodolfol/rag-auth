using RagAuth.Api.Contracts.Business;

namespace RagAuth.Api.Contracts.Dashboard;

public sealed record BusinessDashboardResponse(ClientSummaryResponse Client, BusinessResponse? Business);
