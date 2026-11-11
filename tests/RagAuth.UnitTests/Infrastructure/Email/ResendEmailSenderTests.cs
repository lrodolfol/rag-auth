using System.Net;
using Microsoft.Extensions.Options;
using Moq;
using RagAuth.Api.Configuration;
using RagAuth.Api.Infrastructure.Email;
using Resend;

namespace RagAuth.UnitTests.Infrastructure.Email;

public sealed class ResendConfirmationEmailSenderTests
{
    private readonly Mock<IResend> _resend = new();
    private EmailMessage? _sentMessage;

    public ResendConfirmationEmailSenderTests() =>
        _resend.Setup(resend => resend.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((message, _) => _sentMessage = message)
            .ReturnsAsync(new ResendResponse<Guid>(Guid.NewGuid(), null));

    [Fact]
    public async Task SendMapsEmailToResendMessage()
    {
        await CreateSender().SendAsync(new ConfirmationEmail("ana@exemplo.com", "Assunto", "<p>Corpo</p>"), CancellationToken.None);

        Assert.NotNull(_sentMessage);
        Assert.Equal("nao-responda@ragauth.com.br", _sentMessage.From!.Email);
        Assert.Equal(["ana@exemplo.com"], _sentMessage.To!.Select(address => address.Email));
        Assert.Equal("Assunto", _sentMessage.Subject);
        Assert.Equal("<p>Corpo</p>", _sentMessage.HtmlBody);
    }

    [Fact]
    public async Task UnsuccessfulResponseBecomesFailure()
    {
        var resendException = new ResendException(HttpStatusCode.UnprocessableEntity, ErrorType.ValidationError, "inválido", null);
        _resend.Setup(resend => resend.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResendResponse<Guid>(resendException, null));

        var exception = await Assert.ThrowsAsync<ConfirmationEmailDeliveryException>(
            () => CreateSender().SendAsync(new ConfirmationEmail("ana@exemplo.com", "Assunto", "Corpo"), CancellationToken.None));

        Assert.Same(resendException, exception.InnerException);
    }

    [Fact]
    public async Task SdkExceptionPropagatesToDispatcher()
    {
        _resend.Setup(resend => resend.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Conexão recusada."));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => CreateSender().SendAsync(new ConfirmationEmail("ana@exemplo.com", "Assunto", "Corpo"), CancellationToken.None));
    }

    private ResendConfirmationEmailSender CreateSender() =>
        new(_resend.Object, Options.Create(new EmailOptions { FromAddress = "nao-responda@ragauth.com.br" }));
}
