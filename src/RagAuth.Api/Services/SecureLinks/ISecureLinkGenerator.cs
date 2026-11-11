namespace RagAuth.Api.Services.SecureLinks;

/// <summary>Gera e confere links selector/validator para qualquer ação (research R2).</summary>
public interface ISecureLinkGenerator
{
    string GenerateSelector();

    string GenerateValidator();

    string ComputeValidatorHash(string validator, string accessCode, string purpose);

    bool IsValidatorMatch(string validator, string accessCode, string purpose, string storedHash);
}
