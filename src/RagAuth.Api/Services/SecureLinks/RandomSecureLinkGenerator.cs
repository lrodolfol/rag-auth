using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace RagAuth.Api.Services.SecureLinks;

/// <summary>
/// Padrão selector/validator: o selector localiza o link; só o hash do validator,
/// vinculado ao código de acesso e à ação (<c>purpose</c>), é persistido.
/// </summary>
public sealed class RandomSecureLinkGenerator : ISecureLinkGenerator
{
    public const int SelectorByteCount = 16;
    public const int ValidatorByteCount = 32;
    public const int SelectorLength = 22;
    public const int ValidatorLength = 43;
    public const int ValidatorHashLength = 64;

    public string GenerateSelector() => GenerateRandomBase64Url(SelectorByteCount);

    public string GenerateValidator() => GenerateRandomBase64Url(ValidatorByteCount);

    public string ComputeValidatorHash(string validator, string accessCode, string purpose)
    {
        var hashInput = $"{validator}:{accessCode}:{purpose}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(hashInput));
        return Convert.ToHexStringLower(hashBytes);
    }

    public bool IsValidatorMatch(string validator, string accessCode, string purpose, string storedHash)
    {
        var computedHash = Encoding.ASCII.GetBytes(ComputeValidatorHash(validator, accessCode, purpose));
        var expectedHash = Encoding.ASCII.GetBytes(storedHash);
        return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
    }

    private static string GenerateRandomBase64Url(int byteCount) =>
        Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(byteCount));
}
