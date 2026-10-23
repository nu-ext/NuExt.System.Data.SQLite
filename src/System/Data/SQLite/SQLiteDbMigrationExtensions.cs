namespace System.Data.SQLite;

/// <summary>
/// Provides extension methods for applying SQLite database migrations.
/// </summary>
public static class SQLiteDbMigrationExtensions
{
    /// <summary>
    /// Applies all pending migrations to the default migration scope of a SQLite database.
    /// </summary>
    /// <param name="migrations">The migrations in the order in which they must be applied.</param>
    /// <param name="createConnection">A function that creates a SQLite database connection.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException"><paramref name="migrations"/> or <paramref name="createConnection"/> is <see langword="null"/>.</exception>
    /// <exception cref="DbMigrationException">A migration declares dependencies and must be executed through <see cref="SQLiteDbMigrationPlan"/>, or the migration configuration or applied history is invalid.</exception>
    public static void Migrate(this IEnumerable<SQLiteDbMigration> migrations, Func<SQLiteDbConnection> createConnection, CancellationToken cancellationToken = default)
    {
        MigrateScope(migrations, createConnection, SQLiteMigrationHistory.DefaultScope, null, cancellationToken);
    }

    /// <summary>
    /// Applies all pending migrations to the default migration scope of a SQLite database.
    /// </summary>
    /// <param name="migrations">The migrations in the order in which they must be applied.</param>
    /// <param name="createConnection">A function that creates a SQLite database connection.</param>
    /// <param name="version">Optional version information to record for each applied migration.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException"><paramref name="migrations"/> or <paramref name="createConnection"/> is <see langword="null"/>.</exception>
    /// <exception cref="DbMigrationException">A migration declares dependencies and must be executed through <see cref="SQLiteDbMigrationPlan"/>, or the migration configuration or applied history is invalid.</exception>
    public static void Migrate(this IEnumerable<SQLiteDbMigration> migrations, Func<SQLiteDbConnection> createConnection, string? version, CancellationToken cancellationToken = default)
    {
        MigrateScope(migrations, createConnection, SQLiteMigrationHistory.DefaultScope, version, cancellationToken);
    }

    /// <summary>
    /// Applies all pending migrations to the specified migration scope of a SQLite database.
    /// </summary>
    /// <param name="migrations">The migrations in the order in which they must be applied.</param>
    /// <param name="createConnection">A function that creates a SQLite database connection.</param>
    /// <param name="scope">The stable migration scope identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException"><paramref name="migrations"/> or <paramref name="createConnection"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="scope"/> is <see langword="null"/>, empty, or consists only of white-space characters.</exception>
    /// <exception cref="DbMigrationException">A migration declares dependencies and must be executed through <see cref="SQLiteDbMigrationPlan"/>, or the migration configuration or applied history is invalid.</exception>
    public static void MigrateScope(this IEnumerable<SQLiteDbMigration> migrations, Func<SQLiteDbConnection> createConnection, string scope, CancellationToken cancellationToken = default)
    {
        MigrateScope(migrations, createConnection, scope, null, cancellationToken);
    }

    /// <summary>
    /// Applies all pending migrations to the specified migration scope of a SQLite database.
    /// </summary>
    /// <param name="migrations">The migrations in the order in which they must be applied.</param>
    /// <param name="createConnection">A function that creates a SQLite database connection.</param>
    /// <param name="scope">The stable migration scope identifier.</param>
    /// <param name="version">Optional version information to record for each applied migration.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException"><paramref name="migrations"/> or <paramref name="createConnection"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="scope"/> is <see langword="null"/>, empty, or consists only of white-space characters.</exception>
    /// <exception cref="DbMigrationException">A migration declares dependencies and must be executed through <see cref="SQLiteDbMigrationPlan"/>, or the migration configuration or applied history is invalid.</exception>
    public static void MigrateScope(this IEnumerable<SQLiteDbMigration> migrations, Func<SQLiteDbConnection> createConnection, string scope, string? version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        ArgumentNullException.ThrowIfNull(createConnection);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        cancellationToken.ThrowIfCancellationRequested();

        SQLiteDbMigration[] migrationArray = [.. migrations];

        ValidateIndependentMigrations(migrationArray);

        var executor = new SQLiteMigrationExecutor(createConnection);
        var history = new SQLiteMigrationHistory(scope);
        var migrator = new DbMigrator<SQLiteDbMigrationContext>(executor, history, migrationArray, version);
        migrator.Migrate(cancellationToken);
    }

    /// <summary>
    /// Applies all pending migrations to the default migration scope using an existing SQLite database context.
    /// </summary>
    /// <param name="migrations">The migrations in the order in which they must be applied.</param>
    /// <param name="dbContext">The existing SQLite database context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// The caller owns the database context and is responsible for committing or rolling back its transaction.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="migrations"/> or <paramref name="dbContext"/> is <see langword="null"/>.</exception>
    /// <exception cref="DbMigrationException">A migration declares dependencies and must be executed through <see cref="SQLiteDbMigrationPlan"/>, or the migration configuration or applied history is invalid.</exception>
    public static void Migrate(this IEnumerable<SQLiteDbMigration> migrations, SQLiteDbContext dbContext, CancellationToken cancellationToken = default)
    {
        MigrateScope(migrations, dbContext, SQLiteMigrationHistory.DefaultScope, null, cancellationToken);
    }

    /// <summary>
    /// Applies all pending migrations to the default migration scope using an existing SQLite database context.
    /// </summary>
    /// <param name="migrations">The migrations in the order in which they must be applied.</param>
    /// <param name="dbContext">The existing SQLite database context.</param>
    /// <param name="version">Optional version information to record for each applied migration.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// The caller owns the database context and is responsible for committing or rolling back its transaction.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="migrations"/> or <paramref name="dbContext"/> is <see langword="null"/>.</exception>
    /// <exception cref="DbMigrationException">A migration declares dependencies and must be executed through <see cref="SQLiteDbMigrationPlan"/>, or the migration configuration or applied history is invalid.</exception>
    public static void Migrate(this IEnumerable<SQLiteDbMigration> migrations, SQLiteDbContext dbContext, string? version, CancellationToken cancellationToken = default)
    {
        MigrateScope(migrations, dbContext, SQLiteMigrationHistory.DefaultScope, version, cancellationToken);
    }

    /// <summary>
    /// Applies all pending migrations to the specified migration scope using an existing SQLite database context.
    /// </summary>
    /// <param name="migrations">The migrations in the order in which they must be applied.</param>
    /// <param name="dbContext">The existing SQLite database context.</param>
    /// <param name="scope">The stable migration scope identifier.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// The caller owns the database context and is responsible for committing or rolling back its transaction.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="migrations"/> or <paramref name="dbContext"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="scope"/> is <see langword="null"/>, empty, or consists only of white-space characters.</exception>
    /// <exception cref="DbMigrationException">A migration declares dependencies and must be executed through <see cref="SQLiteDbMigrationPlan"/>, or the migration configuration or applied history is invalid.</exception>
    public static void MigrateScope(this IEnumerable<SQLiteDbMigration> migrations, SQLiteDbContext dbContext, string scope, CancellationToken cancellationToken = default)
    {
        MigrateScope(migrations, dbContext, scope, null, cancellationToken);
    }

    /// <summary>
    /// Applies all pending migrations to the specified migration scope using an existing SQLite database context.
    /// </summary>
    /// <param name="migrations">The migrations in the order in which they must be applied.</param>
    /// <param name="dbContext">The existing SQLite database context.</param>
    /// <param name="scope">The stable migration scope identifier.</param>
    /// <param name="version">Optional version information to record for each applied migration.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <remarks>
    /// The caller owns the database context and is responsible for committing or rolling back its transaction.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="migrations"/> or <paramref name="dbContext"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="scope"/> is <see langword="null"/>, empty, or consists only of white-space characters.</exception>
    /// <exception cref="DbMigrationException">A migration declares dependencies and must be executed through <see cref="SQLiteDbMigrationPlan"/>, or the migration configuration or applied history is invalid.</exception>
    public static void MigrateScope(this IEnumerable<SQLiteDbMigration> migrations, SQLiteDbContext dbContext, string scope, string? version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        cancellationToken.ThrowIfCancellationRequested();

        SQLiteDbMigration[] migrationArray = [.. migrations];

        ValidateIndependentMigrations(migrationArray);

        var executor = new SQLiteDbContextMigrationExecutor(dbContext);
        var history = new SQLiteMigrationHistory(scope);
        var migrator = new DbMigrator<SQLiteDbMigrationContext>(executor, history, migrationArray, version);
        migrator.Migrate(cancellationToken);
    }

    private static void ValidateIndependentMigrations(SQLiteDbMigration[] migrations)
    {
        for (var i = 0; i < migrations.Length; i++)
        {
            var migration = migrations[i] ?? throw new DbMigrationException($"The migration at index {i} is null.");
            var dependencies = migration.Dependencies ?? throw new DbMigrationException($"Migration '{migration.Id}' returned a null dependency collection.");

            if (dependencies.Count > 0)
            {
                throw new DbMigrationException($"Migration '{migration.Id}' declares dependencies and must be executed through '{nameof(SQLiteDbMigrationPlan)}'.");
            }
        }
    }
}