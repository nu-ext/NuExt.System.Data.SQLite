namespace System.Data.SQLite;

/// <summary>
/// Executes SQLite database migrations within an existing database context.
/// </summary>
internal sealed class SQLiteDbContextMigrationExecutor : IDbMigrationExecutor<SQLiteDbMigrationContext>
{
    private readonly SQLiteDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteDbContextMigrationExecutor"/> class.
    /// </summary>
    /// <param name="dbContext">The existing SQLite database context.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dbContext"/> is <see langword="null"/>.</exception>
    internal SQLiteDbContextMigrationExecutor(SQLiteDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public void Execute(Action<SQLiteDbMigrationContext> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        Throw.InvalidOperationExceptionIf(!_dbContext.IsActive, "The database context does not have an active transaction.");

        operation(new SQLiteDbMigrationContext(_dbContext.Connection, cancellationToken));
    }
}