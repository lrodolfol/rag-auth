using System.Security.Cryptography;

namespace RagAuth.Api.Services.Auth;

public sealed class RandomAccessCodeGenerator : IAccessCodeGenerator
{
    public const int AccessCodeLength = 12;

    private const string AllowedCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public string Generate() => RandomNumberGenerator.GetString(AllowedCharacters, AccessCodeLength);
}
