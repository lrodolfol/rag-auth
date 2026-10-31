using RagAuth.Api.Contracts.Business;

namespace RagAuth.Api.Contracts.Auth;

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    int ClientId,
    string AccessCode,
    BusinessResponse? Business);
