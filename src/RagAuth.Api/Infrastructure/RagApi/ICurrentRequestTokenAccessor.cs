namespace RagAuth.Api.Infrastructure.RagApi;

public interface ICurrentRequestTokenAccessor
{
    string? AuthorizationHeader { get; }
}
