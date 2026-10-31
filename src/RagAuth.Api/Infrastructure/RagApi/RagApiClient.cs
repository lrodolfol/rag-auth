using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using RagAuth.Api.Configuration;
using RagAuth.Api.Domain.Exceptions;
using RagAuth.Api.Observability;
using RagAuth.Api.Services.Business;

namespace RagAuth.Api.Infrastructure.RagApi;

public sealed class RagApiClient(
    HttpClient httpClient,
    IOptions<RagApiOptions> ragApiOptions,
    IRagApiResponseTranslator responseTranslator,
    ILogger<RagApiClient> logger) : IRagApiClient
{
    public Task CreateBusinessAsync(string accessCode, NormalizedBusinessData businessData, CancellationToken cancellationToken = default) =>
        SendAsync(RagApiOperations.CreateBusiness, HttpMethod.Post, CreateBusinessBody(accessCode, businessData), cancellationToken);

    public Task UpdateBusinessAsync(string accessCode, NormalizedBusinessData businessData, CancellationToken cancellationToken = default) =>
        SendAsync(RagApiOperations.UpdateBusiness, HttpMethod.Put, CreateBusinessBody(accessCode, businessData), cancellationToken);

    public Task DeleteBusinessAsync(string accessCode, CancellationToken cancellationToken = default) =>
        SendAsync(RagApiOperations.DeleteBusiness, HttpMethod.Delete, new RagBusinessDeletionRequest(accessCode), cancellationToken);

    private static RagBusinessRequest CreateBusinessBody(string accessCode, NormalizedBusinessData businessData) =>
        new(
            accessCode,
            new RagBusinessData(
                businessData.Name,
                businessData.Description,
                businessData.Email,
                businessData.Telephone,
                businessData.Address,
                businessData.Details));

    private async Task SendAsync<TBody>(string operation, HttpMethod method, TBody body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, ragApiOptions.Value.BusinessPath) { Content = JsonContent.Create(body) };
        await request.Content.LoadIntoBufferAsync(cancellationToken);
        using var response = await SendTranslatingTransportFailuresAsync(operation, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw responseTranslator.Translate(operation, response);
        }

        LogMessages.RagApiCallSucceeded(logger, operation, (int)response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendTranslatingTransportFailuresAsync(
        string operation,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            LogMessages.RagApiFailed(logger, operation, "conexão recusada ou serviço inalcançável");
            throw new RagApiUnavailableException();
        }
        catch (TaskCanceledException exception) when (exception.InnerException is TimeoutException)
        {
            LogMessages.RagApiFailed(logger, operation, "tempo limite esgotado");
            throw new RagApiTimeoutException();
        }
    }
}
