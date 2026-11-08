using Microsoft.Extensions.Options;
using RagAuth.Api.Configuration;
using Resend;

namespace RagAuth.Api.Infrastructure.Email;

public static class EmailServiceExtensions
{
    /// <summary>Registra o Resend por DI (sem <c>ResendClient.Create</c>), a fila, o compositor, o remetente e o dispatcher.</summary>
    public static IServiceCollection AddEmailServices(this IServiceCollection services)
    {
        services.AddOptions<ResendClientOptions>()
            .Configure<IOptions<EmailOptions>>((resendOptions, emailOptions) => resendOptions.ApiToken = emailOptions.Value.ResendApiKey);
        services.AddHttpClient<IResend, ResendClient>();
        services.AddSingleton<IConfirmationEmailQueue, ChannelConfirmationEmailQueue>();
        services.AddSingleton<IConfirmationEmailComposer, HtmlConfirmationEmailComposer>();
        services.AddScoped<IConfirmationEmailSender, ResendConfirmationEmailSender>();
        services.AddHostedService<ConfirmationEmailDispatcher>();
        return services;
    }
}
