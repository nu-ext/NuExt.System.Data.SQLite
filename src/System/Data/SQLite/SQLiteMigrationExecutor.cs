namespace System.Data.SQLite;

/// <summary>
/// Executes SQLite database migrations within a synchronized transaction scope.
/// </summary>
internal sealed class SQLiteMigrationExecutor : IDbMigrationExecutor<SQLiteDbMigrationContext>
{
    private readonly Func<SQLiteDbConnection> _createConnection;

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteMigrationExecutor"/> class.
    /// </summary>
    /// <param name="createConnection">A function that creates a SQLite database connection.</param>
    /// <exception cref="ArgumentNullException"><paramref name="createConnection"/> is <see langword="null"/>.</exception>
    internal SQLiteMigrationExecutor(Func<SQLiteDbConnection> createConnection)
    {
        _createConnection = createConnection ?? throw new ArgumentNullException(nameof(createConnection));
    }

    /// <inheritdoc/>
    public void Execute(Action<SQLiteDbMigrationContext> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = _createConnection();

        connection.ExecuteInTransaction(transaction =>
        {
            var context = new SQLiteDbMigrationContext(connection, cancellationToken);
            operation(context);
            transaction.Commit();
        }, cancellationToken);
    }
}