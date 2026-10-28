namespace RagAuth.Api.Services.Business;

public sealed record NormalizedBusinessData(
    string Name,
    string Description,
    string? Email,
    string? Telephone,
    string? Address,
    string? Details);
