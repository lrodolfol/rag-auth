using RagAuth.Api.Contracts.Business;

namespace RagAuth.Api.Services.Business;

public interface IBusinessInputNormalizer
{
    NormalizedBusinessData Normalize(BusinessRequest request);
}

public sealed class BusinessInputNormalizer : IBusinessInputNormalizer
{
    public NormalizedBusinessData Normalize(BusinessRequest request) =>
        new(
            request.Name.Trim(),
            request.Description.Trim(),
            NormalizeOptionalText(request.Email),
            NormalizeTelephone(request.Telephone),
            NormalizeOptionalText(request.Address),
            NormalizeOptionalText(request.Details));

    private static string? NormalizeOptionalText(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string? NormalizeTelephone(string? telephone)
    {
        var digits = new string((telephone ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }
}
