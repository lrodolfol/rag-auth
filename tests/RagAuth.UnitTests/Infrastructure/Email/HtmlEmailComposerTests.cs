using System.Net;
using Microsoft.Extensions.Options;
using RagAuth.Api.Configuration;
using RagAuth.Api.Infrastructure.Email;
using RagAuth.Api.Services.EmailConfirmation;
using RagAuth.UnitTests.TestDoubles;

namespace RagAuth.UnitTests.Infrastructure.Email;

public sealed class HtmlEmailComposerTests
{
    private const string ConfirmationUrl = "https://ragauth.com.br/AAAAAAAAAAAAAAAAAAAAAA/confirmation-register/BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

    private readonly HtmlEmailComposer _composer = new(
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

    [Theory]
    [InlineData(EmailKind.EmailConfirmation, "Confirme seu cadastro no RagAuth")]
    [InlineData(EmailKind.PasswordRecovery, "Redefina sua senha no RagAuth")]
    [InlineData(EmailKind.PasswordChanged, "Sua senha foi alterada no RagAuth")]
    public void ComposeUsesSubjectOfEachKind(EmailKind kind, string expectedSubject)
    {
        var email = _composer.Compose(CreateMessage(kind, "Ana", RecoveryUrl, ChangedAt));

        Assert.Equal(expectedSubject, email.Subject);
    }

    [Theory]
    [InlineData(EmailKind.EmailConfirmation)]
    [InlineData(EmailKind.PasswordRecovery)]
    [InlineData(EmailKind.PasswordChanged)]
    public void ComposeLeavesNoPlaceholderInAnyTemplate(EmailKind kind)
    {
        var email = _composer.Compose(CreateMessage(kind, "Ana", RecoveryUrl, ChangedAt));

        Assert.DoesNotContain("{{", email.HtmlBody);
        Assert.Contains("© 2027 RagAuth. Todos os direitos reservados.", email.HtmlBody);
    }

    [Theory]
    [InlineData(EmailKind.EmailConfirmation)]
    [InlineData(EmailKind.PasswordRecovery)]
    public void ComposeEncodesActionUrlAsHtml(EmailKind kind)
    {
        var email = _composer.Compose(CreateMessage(kind, "Ana", "https://ragauth.com.br/a?b=1&c=<2>", null));

        Assert.Contains("https://ragauth.com.br/a?b=1&amp;c=&lt;2&gt;", email.HtmlBody);
        Assert.DoesNotContain("c=<2>", email.HtmlBody);
    }

    [Fact]
    public void ComposePasswordRecoveryContainsLinkValidityAndIgnoreNotice()
    {
        var email = _composer.Compose(CreateMessage(EmailKind.PasswordRecovery, "Ana", RecoveryUrl, null));

        Assert.Equal(2, CountOccurrences(email.HtmlBody, $"href=\"{RecoveryUrl}\""));
        Assert.Equal(3, CountOccurrences(email.HtmlBody, RecoveryUrl));
        Assert.Contains("Este link é válido por <strong>24 horas</strong>.", email.HtmlBody);
        Assert.Contains("Se o link expirar, solicite uma nova recuperação de senha na página de login.", email.HtmlBody);
        Assert.Contains("Se você não pediu a redefinição de senha, ignore este e-mail: sua senha atual continua valendo.", email.HtmlBody);
    }

    [Fact]
    public void ComposePasswordChangedHasNoResetLinkAndShowsBrasiliaTime()
    {
        var email = _composer.Compose(CreateMessage(EmailKind.PasswordChanged, "Ana", null, ChangedAt));

        Assert.DoesNotContain("href=", email.HtmlBody);
        Assert.DoesNotContain("pass-recuvery", email.HtmlBody);
        Assert.Contains("foi alterada em 01/03/2027 às 09:30.", WebUtility.HtmlDecode(email.HtmlBody));
        Assert.Contains("Se não foi você, solicite imediatamente a recuperação de senha e entre em contato com o suporte.", email.HtmlBody);
    }

    [Fact]
    public void ComposePasswordChangedEncodesClientName()
    {
        var email = _composer.Compose(CreateMessage(EmailKind.PasswordChanged, "<script>x</script>", null, ChangedAt));

        Assert.DoesNotContain("<script>", email.HtmlBody);
        Assert.Contains("&lt;script&gt;", email.HtmlBody);
    }

    [Fact]
    public void ChangedAtUsesFixedUtcMinusThreeWhenTimeZoneIsMissing()
    {
        var fallbackTimeZone = HtmlEmailComposer.ResolveBrasiliaTimeZone("Fuso/Inexistente");

        Assert.Equal(TimeSpan.FromHours(-3), fallbackTimeZone.BaseUtcOffset);
        Assert.Equal("01/03/2027 às 09:30", HtmlEmailComposer.FormatChangedAt(ChangedAt, fallbackTimeZone));
    }

    [Fact]
    public void ChangedAtIsEmptyWhenOccurredAtIsMissing() =>
        Assert.Equal(string.Empty, HtmlEmailComposer.FormatChangedAt(null, TimeZoneInfo.Utc));

    [Fact]
    public void CatalogRejectsUnknownKindAndMissingResource()
    {
        Assert.Throws<InvalidOperationException>(() => EmailTemplateCatalog.For((EmailKind)99));
        Assert.Throws<InvalidOperationException>(() => EmailTemplateCatalog.LoadResource("Inexistente.html"));
    }

    private const string RecoveryUrl = "https://ragauth.com.br/AAAAAAAAAAAAAAAAAAAAAA/pass-recuvery/BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

    private static readonly DateTimeOffset ChangedAt = new(2027, 3, 1, 12, 30, 0, TimeSpan.Zero);

    private static OutgoingEmailMessage CreateMessage(string clientName) =>
        new(EmailKind.EmailConfirmation, 42, "ana@exemplo.com", clientName, ConfirmationUrl);

    private static OutgoingEmailMessage CreateMessage(EmailKind kind, string clientName, string? actionUrl, DateTimeOffset? occurredAt) =>
        new(kind, 42, "ana@exemplo.com", clientName, actionUrl, occurredAt);

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
