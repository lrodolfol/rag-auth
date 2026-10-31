namespace RagAuth.Api.Domain.Entities;

public interface IHasTimestamps
{
    DateTimeOffset CreatedAt { get; set; }

    DateTimeOffset UpdatedAt { get; set; }
}
