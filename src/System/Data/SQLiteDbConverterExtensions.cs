using System.Data.SQLite;
using System.Diagnostics;

namespace System.Data;

public static class SQLiteDbConverterExtensions
{
    public static void Initialize(this IReadOnlyList<SQLiteDbConverter> converters, Func<SQLiteDbConnection> createConnection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(converters);
        ArgumentNullException.ThrowIfNull(createConnection);

        using var connection = createConnection();
        connection.AcquireLock(() =>
        {
            converters.Initialize(connection, cancellationToken);
        }, cancellationToken);
    }

    public static void Initialize(this IReadOnlyList<SQLiteDbConverter> converters, SQLiteDbConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(converters);
        ArgumentNullException.ThrowIfNull(connection);
        Debug.Assert(connection.InTransaction == false, $"{nameof(connection)} in transaction state.");
        Throw.InvalidOperationExceptionIf(connection.InTransaction, $"{nameof(connection)} in transaction state.");
        Debug.Assert(connection.IsAcquired, $"{nameof(connection)} is not acquired.");
        Throw.InvalidOperationExceptionIf(connection.IsAcquired == false, $"{nameof(connection)} is not acquired.");

        connection.ExecuteInTransaction(t =>
        {
            DbConverterExtensions.Initialize(converters, connection, cancellationToken);
            t.Commit();
        }, cancellationToken);
    }
}
