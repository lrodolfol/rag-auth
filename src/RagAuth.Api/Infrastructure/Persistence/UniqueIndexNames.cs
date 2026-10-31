namespace RagAuth.Api.Infrastructure.Persistence;

public static class UniqueIndexNames
{
    public const string ClientAccessCode = "ix_clients_accessCode";
    public const string ActiveClientEmail = "ix_clients_email_active";
    public const string BusinessClientId = "ix_businesses_clientId";
}
