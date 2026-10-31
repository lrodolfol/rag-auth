using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace RagAuth.UnitTests.TestDoubles;

public static class UniqueViolationExceptions
{
    public static DbUpdateException For(string indexName)
    {
        var postgresException = new PostgresException(
            "duplicate key value violates unique constraint",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.UniqueViolation,
            constraintName: indexName);
        return new DbUpdateException("Falha ao gravar.", postgresException);
    }
}
