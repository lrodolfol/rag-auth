namespace RagAuth.Api.Contracts.Dashboard;

public sealed record ClientSummaryResponse(
    int Id,
    string Name,
    string Email,
    bool EmailConfirmed,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
