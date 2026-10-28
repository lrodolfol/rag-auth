using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace RagAuth.Api.Infrastructure.Persistence;

public static class UniqueConstraintViolation
{
    public static bool IsViolationOf(DbUpdateException exception, string indexName) =>
        exception.InnerException is PostgresException postgresException
        && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
        && postgresException.ConstraintName == indexName;
}
