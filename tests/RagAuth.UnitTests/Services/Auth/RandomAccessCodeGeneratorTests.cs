using System.Text.RegularExpressions;
using RagAuth.Api.Services.Auth;

namespace RagAuth.UnitTests.Services.Auth;

public sealed partial class RandomAccessCodeGeneratorTests
{
    private readonly RandomAccessCodeGenerator _generator = new();

    [Fact]
    public void GeneratesTwelveAlphanumericCharacters()
    {
        var accessCode = _generator.Generate();

        Assert.Matches(AlphanumericTwelveCharacters(), accessCode);
    }

    [Fact]
    public void GeneratesTenThousandCodesWithoutRepetition()
    {
        var accessCodes = Enumerable.Range(0, 10_000).Select(_ => _generator.Generate()).ToHashSet();

        Assert.Equal(10_000, accessCodes.Count);
    }

    [GeneratedRegex("^[A-Za-z0-9]{12}$")]
    private static partial Regex AlphanumericTwelveCharacters();
}
