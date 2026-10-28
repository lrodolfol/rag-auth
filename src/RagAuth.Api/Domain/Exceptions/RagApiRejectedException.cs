namespace RagAuth.Api.Domain.Exceptions;

public sealed class RagApiRejectedException(int statusCode)
    : DomainException(statusCode, TitleFor(statusCode), MessageFor(statusCode))
{
    private static string TitleFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Requisição recusada",
        StatusCodes.Status404NotFound => "Recurso não encontrado",
        StatusCodes.Status409Conflict => "Conflito",
        _ => "Operação recusada",
    };

    private static string MessageFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "O serviço de recursos recusou os dados enviados.",
        StatusCodes.Status404NotFound => "O negócio não foi encontrado no serviço de recursos.",
        StatusCodes.Status409Conflict => "O serviço de recursos indicou conflito com os dados enviados.",
        _ => "O serviço de recursos recusou a operação.",
    };
}
