using System.Buffers;
using System.Text;

namespace System.Data.SQLite;

/// <summary>
/// Defines a migration plan that coordinates multiple SQLite migration scopes.
/// </summary>
public sealed class SQLiteDbMigrationPlan
{
    private readonly List<SQLiteDbMigrationScope> _scopes = [];

    /// <summary>
    /// Adds a migration scope to the plan.
    /// </summary>
    /// <param name="scope">The stable migration scope identifier.</param>
    /// <param name="migrations">The migrations in the order in which they must be applied.</param>
    /// <param name="version">Optional version information to record for each applied migration.</param>
    /// <returns>The current migration plan.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="migrations"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="scope"/> is <see langword="null"/>, empty, or consists only of white-space characters.</exception>
    /// <exception cref="InvalidOperationException">The migration scope has already been added.</exception>
    public SQLiteDbMigrationPlan AddScope(string scope, IEnumerable<SQLiteDbMigration> migrations, string? version = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentNullException.ThrowIfNull(migrations);

        if (_scopes.Exists(item => StringComparer.Ordinal.Equals(item.Name, scope)))
        {
            throw new InvalidOperationException($"Migration scope '{scope}' has already been added.");
        }

        SQLiteDbMigration[] migrationArray = [.. migrations];

        ValidateMigrations(scope, migrationArray);

        _scopes.Add(new SQLiteDbMigrationScope(scope, migrationArray, version));
        return this;
    }

    /// <summary>
    /// Applies all pending migrations from the registered migration scopes within a synchronized transaction.
    /// </summary>
    /// <param name="createConnection">A function that creates a SQLite database connection.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException"><paramref name="createConnection"/> is <see langword="null"/>.</exception>
    /// <exception cref="DbMigrationException">The migration plan or applied migration history is invalid, or migration dependencies cannot be satisfied.</exception>
    public void Migrate(Func<SQLiteDbConnection> createConnection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(createConnection);
        cancellationToken.ThrowIfCancellationRequested();

        if (_scopes.Count == 0)
        {
            return;
        }

        using var connection = createConnection();
        connection.AcquireLock(static state =>
        {
            using var dbContext = new SQLiteDbContext(state.Connection);
            try
            {
                state.Plan.Migrate(dbContext, state.CancellationToken);
                dbContext.Commit();
            }
            catch (Exception ex)
            {
                dbContext.TryRollback(ex);
                throw;
            }
        }, (Plan: this, Connection: connection, CancellationToken: cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Applies all pending migrations from the registered migration scopes using an existing SQLite database context.
    /// </summary>
    /// <param name="dbContext">The existing SQLite database context.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dbContext"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The database context does not have an active transaction.</exception>
    /// <exception cref="DbMigrationException">The migration plan or applied migration history is invalid, or migration dependencies cannot be satisfied.</exception>
    /// <remarks>
    /// The caller owns the database context and is responsible for committing or rolling back its transaction.
    /// </remarks>
    public void Migrate(SQLiteDbContext dbContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        cancellationToken.ThrowIfCancellationRequested();
        Throw.InvalidOperationExceptionIf(!dbContext.IsActive, "The database context does not have an active transaction.");

        if (_scopes.Count == 0)
        {
            return;
        }

        ValidateRegisteredDependencies();

        var context = new SQLiteDbMigrationContext(dbContext.Connection, cancellationToken);

        _scopes[0].History.EnsureCreated(context);

        var history = SQLiteMigrationHistory.GetMigrationHistory(context);

        int[]? rentedArray = null;
        var nextMigrationIndexes = _scopes.Count <= 16
            ? stackalloc int[_scopes.Count]
            : (rentedArray = ArrayPool<int>.Shared.Rent(_scopes.Count)).AsSpan(0, _scopes.Count);

        try
        {
            for (var i = 0; i < _scopes.Count; i++)
            {
                var scope = _scopes[i];
                var appliedMigrations = history.GetAppliedMigrations(scope.Name);

                ValidateAppliedMigrations(scope, appliedMigrations);

                nextMigrationIndexes[i] = appliedMigrations.Count;
            }

            ApplyPendingMigrations(context, nextMigrationIndexes, history, cancellationToken);
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<int>.Shared.Return(rentedArray);
            }
        }
    }

    private void ApplyPendingMigrations(SQLiteDbMigrationContext context, Span<int> nextMigrationIndexes, SQLiteMigrationHistorySnapshot history, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var executableScopeIndex = -1;
            for (var i = 0; i < nextMigrationIndexes.Length; i++)
            {
                var scope = _scopes[i];
                var nextMigrationIndex = nextMigrationIndexes[i];
                if (nextMigrationIndex >= scope.Migrations.Length)
                {
                    continue;
                }

                var migration = scope.Migrations[nextMigrationIndex];
                if (AreDependenciesApplied(migration, history))
                {
                    executableScopeIndex = i;
                    break;
                }
            }

            if (executableScopeIndex < 0)
            {
                if (_scopes.HasPendingMigrations(nextMigrationIndexes))
                {
                    ThrowUnsatisfiedDependencies(nextMigrationIndexes, history);
                }

                return;
            }

            ApplyMigration(context, executableScopeIndex, nextMigrationIndexes, history);
        }
    }

    private void ApplyMigration(SQLiteDbMigrationContext context, int scopeIndex, Span<int> nextMigrationIndexes, SQLiteMigrationHistorySnapshot history)
    {
        var scope = _scopes[scopeIndex];
        var migrationIndex = nextMigrationIndexes[scopeIndex];
        var migration = scope.Migrations[migrationIndex];

        migration.Up(context);

        var appliedMigration = new AppliedDbMigration(migrationIndex + 1, migration.Id, DateTimeOffset.UtcNow, scope.Version);

        scope.History.Add(context, appliedMigration, FormatDependencies(migration.Dependencies));
        history.Add(scope.Name, appliedMigration);

        nextMigrationIndexes[scopeIndex]++;
    }

    private static string? FormatDependencies(IReadOnlyList<SQLiteDbMigrationDependency> dependencies)
    {
        if (dependencies.Count == 0)
        {
            return null;
        }

        var result = new ValueStringBuilder(stackalloc char[SQLiteDbConnection.StackallocCharBufferSizeLimit]);
        string delimiter = string.Empty;
        for (var i = 0; i < dependencies.Count; i++)
        {
            result.Append(delimiter);
            delimiter = "; ";

            var dependency = dependencies[i];
            result.Append(dependency.Scope);
            result.Append('/');
            result.Append(dependency.MigrationId);
            if (dependency.OptionalScope)
            {
                result.Append('?');
            }
        }
        return result.ToString();
    }

    private bool AreDependenciesApplied(SQLiteDbMigration migration, SQLiteMigrationHistorySnapshot history)
    {
        var dependencies = migration.Dependencies;

        for (var i = 0; i < dependencies.Count; i++)
        {
            var dependency = dependencies[i];
            if (history.IsMigrationApplied(dependency.Scope, dependency.MigrationId))
            {
                continue;
            }
            if (dependency.OptionalScope && !_scopes.IsScopeRegistered(dependency.Scope) && !history.MigrationScopeExists(dependency.Scope))
            {
                continue;
            }
            return false;
        }

        return true;
    }

    private void ValidateRegisteredDependencies()
    {
        for (var scopeIndex = 0; scopeIndex < _scopes.Count; scopeIndex++)
        {
            var scope = _scopes[scopeIndex];

            for (var migrationIndex = 0; migrationIndex < scope.Migrations.Length; migrationIndex++)
            {
                var migration = scope.Migrations[migrationIndex];
                var dependencies = migration.Dependencies;

                for (var dependencyIndex = 0; dependencyIndex < dependencies.Count; dependencyIndex++)
                {
                    var dependency = dependencies[dependencyIndex];
                    var dependencyScope = _scopes.FindScope(dependency.Scope);

                    if (dependencyScope is null)
                    {
                        continue;
                    }

                    if (!dependencyScope.ContainsMigration(dependency.MigrationId))
                    {
                        throw new DbMigrationException($"Migration '{scope.Name}/{migration.Id}' depends on unknown migration " +
                                                       $"'{dependency.Scope}/{dependency.MigrationId}'.");
                    }
                }
            }
        }
    }

    private void ThrowUnsatisfiedDependencies(ReadOnlySpan<int> nextMigrationIndexes, SQLiteMigrationHistorySnapshot history)
    {
        var message = new ValueStringBuilder(stackalloc char[SQLiteDbConnection.StackallocCharBufferSizeLimit]);
        for (var i = 0; i < nextMigrationIndexes.Length; i++)
        {
            var scope = _scopes[i];
            var nextMigrationIndex = nextMigrationIndexes[i];

            if (nextMigrationIndex >= scope.Migrations.Length)
            {
                continue;
            }

            var migration = scope.Migrations[nextMigrationIndex];
            var migrationDependencies = migration.Dependencies;

            for (var dependencyIndex = 0; dependencyIndex < migrationDependencies.Count; dependencyIndex++)
            {
                var dependency = migrationDependencies[dependencyIndex];

                if (history.IsMigrationApplied(dependency.Scope, dependency.MigrationId))
                {
                    continue;
                }

                if (dependency.OptionalScope && !_scopes.IsScopeRegistered(dependency.Scope) && !history.MigrationScopeExists(dependency.Scope))
                {
                    continue;
                }

                message.Append(message.Length == 0
                    ? "The migration plan cannot continue because the following dependencies are not satisfied: "
                    : ", ");

                message.Append('\'');
                message.Append(scope.Name);
                message.Append('/');
                message.Append(migration.Id);
                message.Append("' requires '");
                message.Append(dependency.Scope);
                message.Append('/');
                message.Append(dependency.MigrationId);
                message.Append('\'');
            }
        }

        if (message.Length == 0)
        {
            throw new DbMigrationException("The migration plan cannot continue because the remaining migrations have cyclic or invalid dependencies.");
        }

        message.Append('.');
        throw new DbMigrationException(message.ToString());
    }

    private static void ValidateAppliedMigrations(SQLiteDbMigrationScope scope, IReadOnlyList<AppliedDbMigration> appliedMigrations)
    {
        if (appliedMigrations.Count > scope.Migrations.Length)
        {
            throw new DbMigrationException($"Migration scope '{scope.Name}' contains migrations that are not present in the current migration configuration.");
        }

        for (var i = 0; i < appliedMigrations.Count; i++)
        {
            var appliedMigration = appliedMigrations[i];
            var configuredMigration = scope.Migrations[i];
            var expectedSequence = i + 1;

            if (appliedMigration.Sequence != expectedSequence)
            {
                throw new DbMigrationException($"Applied migration '{scope.Name}/{appliedMigration.Id}' has sequence {appliedMigration.Sequence}, " +
                                               $"but sequence {expectedSequence} was expected.");
            }

            if (!StringComparer.Ordinal.Equals(appliedMigration.Id, configuredMigration.Id))
            {
                throw new DbMigrationException($"Applied migration '{scope.Name}/{appliedMigration.Id}' at sequence {appliedMigration.Sequence} " +
                                               $"does not match configured migration '{scope.Name}/{configuredMigration.Id}'.");
            }
        }
    }

    private static void ValidateMigrations(string scope, SQLiteDbMigration[] migrations)
    {
        var migrationIds = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < migrations.Length; i++)
        {
            var migration = migrations[i] ?? throw new DbMigrationException($"The migration at index {i} in scope '{scope}' is null.");

            if (string.IsNullOrWhiteSpace(migration.Id))
            {
                throw new DbMigrationException($"The migration at index {i} in scope '{scope}' has an empty identifier.");
            }

            if (!migrationIds.Add(migration.Id))
            {
                throw new DbMigrationException($"Duplicate migration identifier '{migration.Id}' in scope '{scope}'.");
            }

            ValidateDependencies(scope, migration);
        }
    }

    private static void ValidateDependencies(string scope, SQLiteDbMigration migration)
    {
        var dependencies = migration.Dependencies ?? throw new DbMigrationException($"Migration '{scope}/{migration.Id}' returned a null dependency collection.");

        var dependencyKeys = new HashSet<(string Scope, string MigrationId)>();

        for (var i = 0; i < dependencies.Count; i++)
        {
            var dependency = dependencies[i];

            if (string.IsNullOrWhiteSpace(dependency.Scope) || string.IsNullOrWhiteSpace(dependency.MigrationId))
            {
                throw new DbMigrationException($"Dependency at index {i} of migration '{scope}/{migration.Id}' is invalid.");
            }
            if (StringComparer.Ordinal.Equals(scope, dependency.Scope) &&
                StringComparer.Ordinal.Equals(migration.Id, dependency.MigrationId))
            {
                throw new DbMigrationException($"Migration '{scope}/{migration.Id}' cannot depend on itself.");
            }
            if (!dependencyKeys.Add((dependency.Scope, dependency.MigrationId)))
            {
                throw new DbMigrationException($"Duplicate dependency '{dependency.Scope}/{dependency.MigrationId}' in migration '{scope}/{migration.Id}'.");
            }
        }
    }
}