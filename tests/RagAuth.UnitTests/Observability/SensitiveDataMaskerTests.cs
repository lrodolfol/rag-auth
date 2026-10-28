using RagAuth.Api.Observability;

namespace RagAuth.UnitTests.Observability;

public sealed class SensitiveDataMaskerTests
{
    [Theory]
    [InlineData("ana@exemplo.com", "a***@exemplo.com")]
    [InlineData("sem-arroba", "***")]
    [InlineData("@exemplo.com", "***")]
    public void MasksEverythingButFirstCharacterAndDomain(string email, string expectedMask)
    {
        Assert.Equal(expectedMask, SensitiveDataMasker.MaskEmail(email));
    }
}
