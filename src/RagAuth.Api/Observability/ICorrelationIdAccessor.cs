namespace RagAuth.Api.Observability;

public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; }
}
