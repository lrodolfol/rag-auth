using System.Text.Json;
using Microsoft.AspNetCore.Http;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using RagAuth.Api.ErrorHandling;
using RagAuth.Api.Observability;

namespace RagAuth.UnitTests.ErrorHandling;

public sealed class ApiEnvelopeProblemDetailsWriterTests
{
    private const string CorrelationId = "0b6f9a1e-3c1d-4b9e-9d1a-6f0c2a7e5d11";

    [Fact]
    public async Task WritesEnvelopeWithDetailAsMessageAndProblemDetailsAsError()
    {
        var problemDetails = new ProblemDetails { Status = 404, Title = "Recurso não encontrado", Detail = "Não há negócio." };

        var envelope = await WriteAsync(problemDetails, 404);

        Assert.Equal(404, envelope.GetProperty("statusCode").GetInt32());
        Assert.False(envelope.GetProperty("success").GetBoolean());
        Assert.Equal("Não há negócio.", envelope.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, envelope.GetProperty("data").ValueKind);
        var error = envelope.GetProperty("error");
        Assert.Equal("Recurso não encontrado", error.GetProperty("title").GetString());
        Assert.Equal("Não há negócio.", error.GetProperty("detail").GetString());
        Assert.Equal(CorrelationId, error.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task UsesTitleAsMessageWhenThereIsNoDetail()
    {
        var problemDetails = new ProblemDetails { Status = 403, Title = "Acesso negado" };

        var envelope = await WriteAsync(problemDetails, 403);

        Assert.Equal("Acesso negado", envelope.GetProperty("message").GetString());
    }

    [Fact]
    public async Task KeepsValidationErrorsAndFillsStatusAndInstanceFromRequest()
    {
        var errors = new Dictionary<string, string[]> { ["email"] = ["Informe um e-mail válido."] };
        var problemDetails = new HttpValidationProblemDetails(errors) { Title = "Um ou mais campos são inválidos." };

        var envelope = await WriteAsync(problemDetails, 400);

        var error = envelope.GetProperty("error");
        Assert.Equal(400, error.GetProperty("status").GetInt32());
        Assert.Equal("/api/v1/auth/register", error.GetProperty("instance").GetString());
        Assert.Equal("Informe um e-mail válido.", error.GetProperty("errors").GetProperty("email")[0].GetString());
        Assert.Equal("Um ou mais campos são inválidos.", envelope.GetProperty("message").GetString());
    }

    [Fact]
    public async Task AppliesConfiguredProblemDetailsCustomization()
    {
        var problemDetailsOptions = new ProblemDetailsOptions
        {
            CustomizeProblemDetails = context => context.ProblemDetails.Title = "Título customizado",
        };

        var envelope = await WriteAsync(new ProblemDetails { Status = 405 }, 405, problemDetailsOptions);

        Assert.Equal("Título customizado", envelope.GetProperty("error").GetProperty("title").GetString());
    }

    [Fact]
    public void CanWriteEveryProblemDetails()
    {
        var writer = CreateWriter(new ProblemDetailsOptions());

        Assert.True(writer.CanWrite(new ProblemDetailsContext { HttpContext = new DefaultHttpContext() }));
    }

    private static async Task<JsonElement> WriteAsync(
        ProblemDetails problemDetails,
        int responseStatusCode,
        ProblemDetailsOptions? problemDetailsOptions = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/auth/register";
        httpContext.Response.StatusCode = responseStatusCode;
        httpContext.Response.Body = new MemoryStream();
        var writer = CreateWriter(problemDetailsOptions ?? new ProblemDetailsOptions());

        await writer.WriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problemDetails });

        Assert.StartsWith("application/json", httpContext.Response.ContentType);
        httpContext.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(httpContext.Response.Body);
        return document.RootElement.Clone();
    }

    private static ApiEnvelopeProblemDetailsWriter CreateWriter(ProblemDetailsOptions problemDetailsOptions)
    {
        var correlationIdAccessor = new Mock<ICorrelationIdAccessor>();
        correlationIdAccessor.Setup(accessor => accessor.CorrelationId).Returns(CorrelationId);
        return new ApiEnvelopeProblemDetailsWriter(
            correlationIdAccessor.Object,
            Options.Create(problemDetailsOptions),
            Options.Create(new JsonOptions()));
    }
}
