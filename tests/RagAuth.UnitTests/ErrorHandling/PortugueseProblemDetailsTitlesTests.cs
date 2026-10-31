using RagAuth.Api.ErrorHandling;

namespace RagAuth.UnitTests.ErrorHandling;

public sealed class PortugueseProblemDetailsTitlesTests
{
    [Theory]
    [InlineData(400, "Um ou mais campos são inválidos.")]
    [InlineData(401, "Não autenticado")]
    [InlineData(403, "Acesso negado")]
    [InlineData(404, "Recurso não encontrado")]
    [InlineData(405, "Método não permitido")]
    [InlineData(409, "Conflito")]
    [InlineData(415, "Tipo de conteúdo não suportado")]
    [InlineData(500, "Erro interno")]
    [InlineData(502, "Falha de integração")]
    [InlineData(503, "Serviço indisponível")]
    [InlineData(504, "Tempo esgotado")]
    public void ReturnsPortugueseTitleForMappedStatus(int statusCode, string expectedTitle)
    {
        Assert.Equal(expectedTitle, PortugueseProblemDetailsTitles.ForStatus(statusCode));
    }

    [Theory]
    [InlineData(418)]
    [InlineData(422)]
    public void ReturnsDefaultTitleForUnknownStatus(int statusCode)
    {
        Assert.Equal("Erro na requisição", PortugueseProblemDetailsTitles.ForStatus(statusCode));
    }
}
