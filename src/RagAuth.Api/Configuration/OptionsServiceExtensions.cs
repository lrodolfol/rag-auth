namespace RagAuth.Api.Configuration;

public static class OptionsServiceExtensions
{
    public static IServiceCollection AddApplicationOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .PostConfigure(options => options.Password = ReadSecret(configuration, EnvironmentSecretNames.DatabasePassword))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .PostConfigure(options => options.SigningKey = ReadSecret(configuration, EnvironmentSecretNames.JwtSigningKey))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<RagApiOptions>()
            .Bind(configuration.GetSection(RagApiOptions.SectionName))
            .PostConfigure(options => options.ApiKey = ReadSecret(configuration, EnvironmentSecretNames.RagApiKey))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return services.AddEmailOptions(configuration);
    }

    private static IServiceCollection AddEmailOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .PostConfigure(options => CompleteEmailOptions(options, configuration))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return services;
    }

    private static void CompleteEmailOptions(EmailOptions options, IConfiguration configuration)
    {
        options.ResendApiKey = ReadSecret(configuration, EnvironmentSecretNames.ResendApiKey);
        if (options.RetryDelaysSeconds.Length == 0)
        {
            options.RetryDelaysSeconds = EmailOptions.DefaultRetryDelaysSeconds;
        }
    }

    private static string ReadSecret(IConfiguration configuration, string secretName) =>
        configuration[secretName] ?? string.Empty;
}
