namespace RagAuth.Api.Services.Business;

public sealed record NormalizedBusinessData(
    string Name,
    string Descricao,
    string? Email,
    string? Telephone,
    string? Address,
    string? detalhes);
