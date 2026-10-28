namespace RagAuth.Api.Domain.Exceptions;

public abstract class DomainException(int statusCode, string title, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public string Title { get; } = title;
}
