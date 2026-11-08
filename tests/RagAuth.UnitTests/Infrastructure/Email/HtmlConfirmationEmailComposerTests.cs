using Microsoft.Extensions.Options;
using RagAuth.Api.Configuration;
using RagAuth.Api.Infrastructure.Email;
using RagAuth.Api.Services.EmailConfirmation;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Infrastructure.Email;

public sealed class HtmlConfirmationEmailComposerTests
{
    private const string ConfirmationUrl = "https://ragauth.com.br/AAAAAAAAAAAAAAAAAAAAAA/confirmation-register/BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

    private readonly HtmlConfirmationEmailComposer _composer = new(
        Options.Create(new EmailOptions { AppName = "RagAuth" }),
        new AdjustableTimeProvider(new DateTimeOffset(2027, 3, 1, 0, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void ComposeUsesSubjectWithApplicationNameAndRecipient()
    {
        var email = _composer.Compose(CreateMessage("Ana"));

        Assert.Equal("Confirme seu cadastro no RagAuth", email.Subject);
        Assert.Equal("ana@exemplo.com", email.RecipientEmail);
    }

    [Fact]
    public void ComposeEncodesClientNameAsHtml()
    {
        var email = _composer.Compose(CreateMessage("<script>alert('x')</script>"));

        Assert.DoesNotContain("<script>", email.HtmlBody);
        Assert.Contains("&lt;script&gt;", email.HtmlBody);
    }

    [Fact]
    public void ComposePlacesUrlInBothLinksAndInVisibleText()
    {
        var email = _composer.Compose(CreateMessage("Ana"));

        Assert.Equal(2, CountOccurrences(email.HtmlBody, $"href=\"{ConfirmationUrl}\""));
        Assert.Equal(3, CountOccurrences(email.HtmlBody, ConfirmationUrl));
    }

    [Fact]
    public void ComposeFillsFooterAndNewLinkGuidanceWithoutLeftoverPlaceholders()
    {
        var email = _composer.Compose(CreateMessage("Ana"));

        Assert.Contains("© 2027 RagAuth. Todos os direitos reservados.", email.HtmlBody);
        Assert.Contains("Este link é válido por <strong>24 horas</strong>.", email.HtmlBody);
        Assert.Contains("Se o link expirar, solicite um novo link de confirmação na página de login.", email.HtmlBody);
        Assert.Contains("Olá, <strong>Ana</strong>!", email.HtmlBody);
        Assert.DoesNotContain("{{", email.HtmlBody);
    }

    private static ConfirmationEmailMessage CreateMessage(string clientName) =>
        new(42, "ana@exemplo.com", clientName, ConfirmationUrl);

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + 1, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
