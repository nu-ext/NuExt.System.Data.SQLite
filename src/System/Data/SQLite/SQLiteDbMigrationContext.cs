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

    /// <summary>
    /// Gets the SQLite database connection used to execute the migration.
    /// </summary>
    public SQLiteDbConnection Connection { get; }

    /// <summary>
    /// Executes the specified SQL statement within the current migration scope.
    /// </summary>
    /// <param name="commandText">The SQL statement to execute.</param>
    /// <returns>The number of rows affected.</returns>
    /// <exception cref="ArgumentException"><paramref name="commandText"/> is <see langword="null"/> or empty.</exception>
    public int ExecuteNonQuery(string commandText)
    {
        return Connection.ExecuteNonQuery(commandText, CancellationToken);
    }

    /// <summary>
    /// Determines whether the specified migration scope has applied migrations.
    /// </summary>
    /// <param name="scope">The stable migration scope identifier.</param>
    /// <returns><see langword="true"/> if the migration scope has applied migrations; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="scope"/> is <see langword="null"/>, empty, or consists only of white-space characters.</exception>
    public bool MigrationScopeExists(string scope)
    {
        return SQLiteMigrationHistory.MigrationScopeExists(this, scope);
    }

    /// <summary>
    /// Determines whether the specified migration has been applied within the specified migration scope.
    /// </summary>
    /// <param name="scope">The stable migration scope identifier.</param>
    /// <param name="migrationId">The migration identifier.</param>
    /// <returns><see langword="true"/> if the migration has been applied; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="scope"/> or <paramref name="migrationId"/> is <see langword="null"/>, empty, or consists only of white-space characters.</exception>
    public bool IsMigrationApplied(string scope, string migrationId)
    {
        return SQLiteMigrationHistory.IsMigrationApplied(this, scope, migrationId);
    }

    /// <summary>
    /// Requires the specified migration to have been applied within the specified migration scope.
    /// </summary>
    /// <param name="scope">The stable migration scope identifier.</param>
    /// <param name="migrationId">The required migration identifier.</param>
    /// <exception cref="ArgumentException"><paramref name="scope"/> or <paramref name="migrationId"/> is <see langword="null"/>, empty, or consists only of white-space characters.</exception>
    /// <exception cref="DbMigrationException">The required migration has not been applied.</exception>
    public void RequireAppliedMigration(string scope, string migrationId)
    {
        if (!IsMigrationApplied(scope, migrationId))
        {
            throw new DbMigrationException($"Migration '{scope}/{migrationId}' must be applied before this migration.");
        }
    }

    /// <summary>
    /// Requires the specified migration when the migration scope has applied migrations.
    /// </summary>
    /// <param name="scope">The stable migration scope identifier.</param>
    /// <param name="migrationId">The required migration identifier.</param>
    /// <exception cref="ArgumentException"><paramref name="scope"/> or <paramref name="migrationId"/> is <see langword="null"/>, empty, or consists only of white-space characters.</exception>
    /// <exception cref="DbMigrationException">The migration scope exists, but the required migration has not been applied.</exception>
    public void RequireAppliedMigrationIfScopeExists(string scope, string migrationId)
    {
        if (MigrationScopeExists(scope))
        {
            RequireAppliedMigration(scope, migrationId);
        }
    }
}