using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RagAuth.Api.Contracts;

namespace RagAuth.IntegrationTests.Infrastructure;

public static class ApiEnvelopeAssertions
{
    public static async Task<ApiResponse<JsonElement>> ReadEnvelopeAsync(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(JsonSerializerOptions.Web);
        Assert.NotNull(envelope);
        return envelope;
    }

    public static async Task<JsonElement> AssertSuccessAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string? expectedMessage = null)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        var envelope = await ReadEnvelopeAsync(response);
        Assert.Equal((int)expectedStatus, envelope.StatusCode);
        Assert.True(envelope.Success);
        Assert.False(string.IsNullOrWhiteSpace(envelope.Message));
        if (expectedMessage is not null)
        {
            Assert.Equal(expectedMessage, envelope.Message);
        }

        Assert.True(envelope.Error is null || ((JsonElement)envelope.Error).ValueKind == JsonValueKind.Null);
        return envelope.Data;
    }

    public static async Task<JsonElement> AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string? expectedMessage = null)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        var envelope = await ReadEnvelopeAsync(response);
        Assert.Equal((int)expectedStatus, envelope.StatusCode);
        Assert.False(envelope.Success);
        Assert.Equal(JsonValueKind.Null, envelope.Data.ValueKind);
        if (expectedMessage is not null)
        {
            Assert.Equal(expectedMessage, envelope.Message);
        }

        var error = Assert.IsType<JsonElement>(envelope.Error);
        Assert.Equal((int)expectedStatus, error.GetProperty("status").GetInt32());
        return error;
    }

    public static void AssertFieldError(JsonElement error, string fieldName)
    {
        var errors = error.GetProperty("errors");
        var fieldExists = errors.TryGetProperty(fieldName, out _);
        Assert.True(fieldExists, $"O campo '{fieldName}' não está em error.errors: {errors}");
    }
}
