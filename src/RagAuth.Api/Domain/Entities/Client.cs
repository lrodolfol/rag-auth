namespace RagAuth.Api.Domain.Entities;

public class Client : IHasTimestamps
{
    public int Id { get; set; }

    public string AccessCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public Business? Business { get; set; }
}
