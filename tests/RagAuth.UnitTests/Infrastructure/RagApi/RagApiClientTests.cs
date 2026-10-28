using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RagAuth.Api.Configuration;
using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.Infrastructure.RagApi;
using RagAuth.Api.Services.Business;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Infrastructure.RagApi;

public sealed class RagApiClientTests
{
    private static readonly NormalizedBusinessData BusinessData =
        new("Padaria da Ana", "Pães artesanais.", "contato@padaria.com", "11987654321", "Rua das Flores, 123", null);

    private readonly Mock<IRagApiResponseTranslator> _responseTranslator = new();

    [Fact]
    public async Task CreateSendsPostWithSnakeCaseBody()
    {
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.Created);

        await CreateClient(handler).CreateBusinessAsync("Ab3dEf6hIj9k", BusinessData);

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/business", handler.LastRequest.RequestUri!.AbsolutePath);
        AssertBusinessBody(handler.LastRequestBody!);
    }

    [Fact]
    public async Task UpdateSendsPutWithSnakeCaseBody()
    {
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK);

        await CreateClient(handler).UpdateBusinessAsync("Ab3dEf6hIj9k", BusinessData);

        Assert.Equal(HttpMethod.Put, handler.LastRequest!.Method);
        AssertBusinessBody(handler.LastRequestBody!);
    }

    [Fact]
    public async Task DeleteSendsDeleteWithOnlyUserCode()
    {
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.NoContent);

        await CreateClient(handler).DeleteBusinessAsync("Ab3dEf6hIj9k");

        Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
        Assert.Equal(handler.LastRequestBody!.Length, handler.LastRequestContentLength);
        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var property = Assert.Single(body.RootElement.EnumerateObject());
        Assert.Equal("user_code", property.Name);
        Assert.Equal("Ab3dEf6hIj9k", property.Value.GetString());
    }

    [Fact]
    public async Task NonSuccessResponseThrowsTranslatedException()
    {
        _responseTranslator.Setup(translator => translator.Translate(RagApiOperations.CreateBusiness, It.IsAny<HttpResponseMessage>()))
            .Returns(new RagApiIntegrationException());
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<RagApiIntegrationException>(() => CreateClient(handler).CreateBusinessAsync("Ab3dEf6hIj9k", BusinessData));
    }

    private static void AssertBusinessBody(string requestBody)
    {
        using var body = JsonDocument.Parse(requestBody);
        Assert.Equal("Ab3dEf6hIj9k", body.RootElement.GetProperty("user_code").GetString());
        var request = body.RootElement.GetProperty("request");
        Assert.Equal("Padaria da Ana", request.GetProperty("name").GetString());
        Assert.Equal("Pães artesanais.", request.GetProperty("description").GetString());
        Assert.Equal("contato@padaria.com", request.GetProperty("email").GetString());
        Assert.Equal("11987654321", request.GetProperty("telephone").GetString());
        Assert.Equal("Rua das Flores, 123", request.GetProperty("address").GetString());
        Assert.Equal(JsonValueKind.Null, request.GetProperty("details").ValueKind);
    }

    private RagApiClient CreateClient(StubHttpMessageHandler handler) =>
        RagApiClientFactory.Create(handler, _responseTranslator.Object, LoggerMocks.Create<RagApiClient>().Object);
}

internal static class RagApiClientFactory
{
    public static RagApiClient Create(HttpMessageHandler handler, IRagApiResponseTranslator translator, ILogger<RagApiClient> logger)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://rag-api.teste") };
        var options = Options.Create(new RagApiOptions { BaseUrl = "http://rag-api.teste", BusinessPath = "/business", ApiKey = "chave" });
        return new RagApiClient(httpClient, options, translator, logger);
    }
}
