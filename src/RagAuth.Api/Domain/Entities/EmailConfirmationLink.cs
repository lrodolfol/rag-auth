namespace RagAuth.Api.Domain.Entities;

public class EmailConfirmationLink
{
    public const string EmailConfirmationPurpose = "email-confirmation";

    /// <summary>Prazo fixo de validade do link, igual ao texto do e-mail (FR-006).</summary>
    public static readonly TimeSpan ValidityPeriod = TimeSpan.FromHours(24);

    public long Id { get; set; }

    public int ClientId { get; set; }

    public Client Client { get; set; } = null!;

    public string Selector { get; set; } = string.Empty;

    public string ValidatorHash { get; set; } = string.Empty;

    public string Purpose { get; set; } = EmailConfirmationPurpose;

    public string Origin { get; set; } = ConfirmationLinkOrigin.Registration;

    public DateTimeOffset IssuedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
