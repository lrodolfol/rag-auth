using Moq;
using RagAuth.Api.Infrastructure.Persistence;

namespace RagAuth.UnitTests.TestDoubles;

public sealed class UnitOfWorkMocks
{
    public UnitOfWorkMocks()
    {
        UnitOfWork.Setup(unitOfWork => unitOfWork.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Transaction.Object);
    }

    public Mock<IUnitOfWork> UnitOfWork { get; } = new();

    public Mock<IUnitOfWorkTransaction> Transaction { get; } = new();

    public void VerifyCommitted()
    {
        Transaction.Verify(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Once());
        Transaction.Verify(transaction => transaction.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    public void VerifyRolledBack()
    {
        Transaction.Verify(transaction => transaction.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once());
        Transaction.Verify(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Never());
    }
}
