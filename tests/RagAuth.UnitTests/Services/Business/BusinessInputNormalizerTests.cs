using RagAuth.Api.Contracts.Business;
using RagAuth.Api.Services.Business;

namespace RagAuth.UnitTests.Services.Business;

public sealed class BusinessInputNormalizerTests
{
    private readonly BusinessInputNormalizer _normalizer = new();

    [Fact]
    public void TrimsTextsAndKeepsOnlyTelephoneDigits()
    {
        var request = new BusinessRequest
        {
            Name = "  Padaria da Ana ",
            Description = " Pães artesanais. ",
            Email = " contato@padaria.com ",
            Telephone = "(11) 98765-4321",
            Address = " Rua das Flores, 123 ",
            Details = " Aberto todos os dias. ",
        };

        var normalized = _normalizer.Normalize(request);

        Assert.Equal(
            new NormalizedBusinessData("Padaria da Ana", "Pães artesanais.", "contato@padaria.com", "11987654321", "Rua das Flores, 123", "Aberto todos os dias."),
            normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TurnsEmptyOptionalFieldsIntoNull(string? emptyValue)
    {
        var request = new BusinessRequest
        {
            Name = "Padaria",
            Description = "Pães",
            Email = emptyValue,
            Telephone = emptyValue,
            Address = emptyValue,
            Details = emptyValue,
        };

        var normalized = _normalizer.Normalize(request);

        Assert.Equal(new NormalizedBusinessData("Padaria", "Pães", null, null, null, null), normalized);
    }
}
