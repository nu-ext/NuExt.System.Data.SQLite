namespace System.Data.SQLite;

/// <summary>
/// Provides extension methods for applying SQLite database migrations.
/// </summary>
public static class SQLiteDbMigrationExtensions
{
    /// <summary>
    /// Applies all pending migrations to a SQLite database.
    /// </summary>
    /// <param name="migrations">The migrations in the order in which they must be applied.</param>
    /// <param name="createConnection">A function that creates a SQLite database connection.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException"><paramref name="migrations"/> or <paramref name="createConnection"/> is <see langword="null"/>.</exception>
    public static void Migrate(this IEnumerable<SQLiteDbMigration> migrations, Func<SQLiteDbConnection> createConnection, CancellationToken cancellationToken = default)
    {
        Migrate(migrations, createConnection, null, cancellationToken);
    }

    /// <summary>
    /// Applies all pending migrations to a SQLite database.
    /// </summary>
    /// <param name="migrations">The migrations in the order in which they must be applied.</param>
    /// <param name="createConnection">A function that creates a SQLite database connection.</param>
    /// <param name="version">Optional version information to record for each applied migration.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException"><paramref name="migrations"/> or <paramref name="createConnection"/> is <see langword="null"/>.</exception>
    public static void Migrate(this IEnumerable<SQLiteDbMigration> migrations, Func<SQLiteDbConnection> createConnection, string? version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        ArgumentNullException.ThrowIfNull(createConnection);
        cancellationToken.ThrowIfCancellationRequested();

        var executor = new SQLiteMigrationExecutor(createConnection);
        var history = new SQLiteMigrationHistory();
        var migrator = new DbMigrator<SQLiteDbMigrationContext>(executor, history, migrations, version);
        migrator.Migrate(cancellationToken);
    }

    /// <summary>
    /// Applies all pending migrations using an existing SQLite database context.
    /// </summary>
    /// <param name="migrations">The migrations in the order in which they must be applied.</param>
    /// <param name="dbContext">The existing SQLite database context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException"><paramref name="migrations"/> or <paramref name="dbContext"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The caller owns the database context and is responsible for committing or rolling back its transaction.
    /// </remarks>
    public static void Migrate(this IEnumerable<SQLiteDbMigration> migrations, SQLiteDbContext dbContext, CancellationToken cancellationToken = default)
    {
        Migrate(migrations, dbContext, null, cancellationToken);
    }

    /// <summary>
    /// Applies all pending migrations using an existing SQLite database context.
    /// </summary>
    /// <param name="migrations">The migrations in the order in which they must be applied.</param>
    /// <param name="dbContext">The existing SQLite database context.</param>
    /// <param name="version">Optional version information to record for each applied migration.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException"><paramref name="migrations"/> or <paramref name="dbContext"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The caller owns the database context and is responsible for committing or rolling back its transaction.
    /// </remarks>
    public static void Migrate(this IEnumerable<SQLiteDbMigration> migrations, SQLiteDbContext dbContext, string? version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        ArgumentNullException.ThrowIfNull(dbContext);
        cancellationToken.ThrowIfCancellationRequested();

        var executor = new SQLiteDbContextMigrationExecutor(dbContext);
        var history = new SQLiteMigrationHistory();
        var migrator = new DbMigrator<SQLiteDbMigrationContext>(executor, history, migrations, version);
        migrator.Migrate(cancellationToken);
    }
}