namespace RagAuth.Api.Domain.Entities;

public class PasswordRecoveryLink
{
    public const string PasswordRecoveryPurpose = "password-recovery";

    /// <summary>Prazo fixo de validade do link, igual ao texto do e-mail.</summary>
    public static readonly TimeSpan ValidityPeriod = TimeSpan.FromHours(24);

    public long Id { get; set; }

    public int ClientId { get; set; }

    public Client Client { get; set; } = null!;

    public string Selector { get; set; } = string.Empty;

    public string ValidatorHash { get; set; } = string.Empty;

    public string Purpose { get; set; } = PasswordRecoveryPurpose;

    public DateTimeOffset IssuedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
