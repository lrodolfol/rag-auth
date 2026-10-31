using RagAuth.Api.Domain.Exceptions;

namespace RagAuth.Api.Infrastructure.RagApi;

public interface IRagApiResponseTranslator
{
    DomainException Translate(string operation, HttpResponseMessage unsuccessfulResponse);
}
