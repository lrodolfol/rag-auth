namespace RagAuth.Api.Services.PasswordRecovery;

/// <summary>Garante uma duração mínima para a operação, para que o tempo de resposta não revele o caminho seguido (SC-003).</summary>
public interface IMinimumResponseDurationGuard
{
    Task RunAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken);
}
