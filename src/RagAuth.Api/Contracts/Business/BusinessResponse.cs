namespace RagAuth.Api.Contracts.Business;

public sealed record BusinessResponse(
    string Name,
    string Description,
    string? Email,
    string? Telephone,
    string? Address,
    string? Details,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
