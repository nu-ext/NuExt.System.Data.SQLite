namespace System.Data.SQLite;

/// <summary>
/// Provides the execution context for SQLite database migrations.
/// </summary>
public sealed class SQLiteDbMigrationContext : DbMigrationContext
{
    internal SQLiteDbMigrationContext(SQLiteDbConnection connection, CancellationToken cancellationToken) : base(cancellationToken)
    {
        Connection = connection;
    }

    internal SQLiteDbConnection Connection { get; }

    /// <summary>
    /// Executes the specified SQL statement within the current migration scope.
    /// </summary>
    /// <param name="commandText">The SQL statement to execute.</param>
    /// <returns>The number of rows affected.</returns>
    /// <exception cref="ArgumentException"><paramref name="commandText"/> is <see langword="null"/> or empty.</exception>
    public int ExecuteNonQuery(string commandText)
    {
        return Connection.ExecuteNonQuery(commandText, cancellationToken: CancellationToken);
    }
}
