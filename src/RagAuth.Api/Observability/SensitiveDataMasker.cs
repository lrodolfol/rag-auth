namespace RagAuth.Api.Observability;

public static class SensitiveDataMasker
{
    public static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
        {
            return "***";
        }

        return string.Concat(email.AsSpan(0, 1), "***", email.AsSpan(atIndex));
    }
}
