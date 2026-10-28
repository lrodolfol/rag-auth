using RagAuth.Api.Contracts.Validation;

namespace RagAuth.UnitTests.Contracts.Validation;

public sealed class SingleLineAttributeTests
{
    private readonly SingleLineAttribute _attribute = new();

    [Theory]
    [InlineData("Rua das Flores, 123")]
    [InlineData(null)]
    public void AcceptsSingleLineOrMissingValue(string? address)
    {
        Assert.True(_attribute.IsValid(address));
    }

    [Theory]
    [InlineData("Rua das Flores\n123")]
    [InlineData("Rua das Flores\r123")]
    public void RejectsLineBreaks(string address)
    {
        Assert.False(_attribute.IsValid(address));
    }

    [Fact]
    public void UsesPortugueseErrorMessage()
    {
        Assert.Equal("O endereço deve ser informado em uma única linha.", _attribute.FormatErrorMessage("address"));
    }
}
