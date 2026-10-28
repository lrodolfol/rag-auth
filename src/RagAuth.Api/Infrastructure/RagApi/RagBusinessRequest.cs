using System.Text.Json.Serialization;

namespace RagAuth.Api.Infrastructure.RagApi;

public sealed record RagBusinessRequest(
    [property: JsonPropertyName("user_code")] string UserCode,
    [property: JsonPropertyName("request")] RagBusinessData Request);

public sealed record RagBusinessData(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("telephone")] string? Telephone,
    [property: JsonPropertyName("address")] string? Address,
    [property: JsonPropertyName("details")] string? Details);
