namespace RagAuth.Api.Infrastructure.Persistence;

public static class UniqueIndexNames
{
    public const string ClientAccessCode = "ix_clients_accessCode";
    public const string ActiveClientEmail = "ix_clients_email_active";
    public const string BusinessClientId = "ix_businesses_clientId";
    public const string EmailConfirmationLinkSelector = "ux_email_confirmation_links_selector";
    public const string ActiveEmailConfirmationLinkPerClient = "ux_email_confirmation_links_active_per_client";
    public const string PasswordRecoveryLinkSelector = "ux_password_recovery_links_selector";
    public const string ActivePasswordRecoveryLinkPerClient = "ux_password_recovery_links_active_per_client";
}
