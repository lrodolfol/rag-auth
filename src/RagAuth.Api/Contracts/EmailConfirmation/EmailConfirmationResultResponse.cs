namespace RagAuth.Api.Contracts.EmailConfirmation;

public sealed record EmailConfirmationResultResponse(string Status)
{
    public const string ConfirmedStatus = "confirmed";
    public const string AlreadyConfirmedStatus = "alreadyConfirmed";
}
