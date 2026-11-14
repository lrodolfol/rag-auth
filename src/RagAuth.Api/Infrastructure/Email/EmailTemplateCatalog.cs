using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace RagAuth.Api.Infrastructure.Email;

public sealed record EmailTemplate(string ResourceName, string SubjectTemplate);

/// <summary>Modelo e assunto de cada <see cref="EmailKind"/>. O HTML de cada modelo é carregado uma única vez.</summary>
public static class EmailTemplateCatalog
{
    public const string ResourcePrefix = "RagAuth.Api.Infrastructure.Email.Templates.";

    public static readonly FrozenDictionary<EmailKind, EmailTemplate> Templates = new Dictionary<EmailKind, EmailTemplate>
    {
        [EmailKind.EmailConfirmation] = new(ResourcePrefix + "ConfirmationEmail.html", "Confirme seu cadastro no {0}"),
        [EmailKind.PasswordRecovery] = new(ResourcePrefix + "PasswordRecoveryEmail.html", "Redefina sua senha no {0}"),
        [EmailKind.PasswordChanged] = new(ResourcePrefix + "PasswordChangedEmail.html", "Sua senha foi alterada no {0}"),
    }.ToFrozenDictionary();

    private static readonly ConcurrentDictionary<EmailKind, Lazy<string>> LoadedBodies = new();

    public static EmailTemplate For(EmailKind kind) =>
        Templates.TryGetValue(kind, out var template)
            ? template
            : throw new InvalidOperationException($"Não existe modelo de e-mail cadastrado para o tipo {kind}.");

    public static string LoadBody(EmailKind kind) =>
        LoadedBodies.GetOrAdd(kind, key => new Lazy<string>(() => LoadResource(For(key).ResourceName))).Value;

    internal static string LoadResource(string resourceName)
    {
        using var stream = typeof(EmailTemplateCatalog).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"O modelo de e-mail '{resourceName}' não foi encontrado nos recursos da aplicação.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
