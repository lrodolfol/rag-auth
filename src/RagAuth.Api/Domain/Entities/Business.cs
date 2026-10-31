namespace RagAuth.Api.Domain.Entities;

public class Business : IHasTimestamps
{
    public int Id { get; set; }

    public int ClientId { get; set; }

    public Client Client { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Telephone { get; set; }

    public string? Address { get; set; }

    public string? Details { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
