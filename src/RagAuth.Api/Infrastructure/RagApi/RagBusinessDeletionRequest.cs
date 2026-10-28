using System.Text.Json.Serialization;

namespace RagAuth.Api.Infrastructure.RagApi;

public sealed record RagBusinessDeletionRequest([property: JsonPropertyName("user_code")] string UserCode);
