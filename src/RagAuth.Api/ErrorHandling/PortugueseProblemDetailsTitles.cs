using System.Collections.Frozen;

namespace RagAuth.Api.ErrorHandling;

public static class PortugueseProblemDetailsTitles
{
    public const string DefaultTitle = "Erro na requisição";

    private static readonly FrozenDictionary<int, string> TitlesByStatusCode = new Dictionary<int, string>
    {
        [StatusCodes.Status400BadRequest] = "Um ou mais campos são inválidos.",
        [StatusCodes.Status401Unauthorized] = "Não autenticado",
        [StatusCodes.Status403Forbidden] = "Acesso negado",
        [StatusCodes.Status404NotFound] = "Recurso não encontrado",
        [StatusCodes.Status405MethodNotAllowed] = "Método não permitido",
        [StatusCodes.Status409Conflict] = "Conflito",
        [StatusCodes.Status415UnsupportedMediaType] = "Tipo de conteúdo não suportado",
        [StatusCodes.Status500InternalServerError] = "Erro interno",
        [StatusCodes.Status502BadGateway] = "Falha de integração",
        [StatusCodes.Status503ServiceUnavailable] = "Serviço indisponível",
        [StatusCodes.Status504GatewayTimeout] = "Tempo esgotado",
    }.ToFrozenDictionary();

    public static string ForStatus(int statusCode) =>
        TitlesByStatusCode.GetValueOrDefault(statusCode, DefaultTitle);
}
