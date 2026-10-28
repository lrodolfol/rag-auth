namespace RagAuth.Api.Contracts.Auth;

public sealed record RegisteredClientResponse(
    int ClientId,
    string AccessCode,
    string Name,
    string Email,
    DateTimeOffset CreatedAt);
