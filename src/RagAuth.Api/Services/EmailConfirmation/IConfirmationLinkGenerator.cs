namespace RagAuth.Api.Services.EmailConfirmation;

public interface IConfirmationLinkGenerator
{
    string GenerateSelector();

    string GenerateValidator();

    string ComputeValidatorHash(string validator, string accessCode);

    bool IsValidatorMatch(string validator, string accessCode, string storedHash);
}
