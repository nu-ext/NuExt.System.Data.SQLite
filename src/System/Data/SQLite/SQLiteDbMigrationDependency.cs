namespace System.Data.SQLite;

/// <summary>
/// Represents a dependency on a migration from another migration scope.
/// </summary>
public readonly record struct SQLiteDbMigrationDependency
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteDbMigrationDependency"/> class.
    /// </summary>
    /// <param name="scope">The stable migration scope identifier.</param>
    /// <param name="migrationId">The required migration identifier.</param>
    /// <param name="optionalScope">
    /// <see langword="true"/> if the dependency is ignored when the scope is neither registered nor present in migration history;
    /// otherwise, <see langword="false"/>.
    /// </param>
    public SQLiteDbMigrationDependency(string scope, string migrationId, bool optionalScope = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationId);

        Scope = scope;
        MigrationId = migrationId;
        OptionalScope = optionalScope;
    }

    /// <summary>
    /// Gets the stable migration scope identifier.
    /// </summary>
    public string Scope { get; }

    /// <summary>
    /// Gets the required migration identifier.
    /// </summary>
    public string MigrationId { get; }

    /// <summary>
    /// Gets a value indicating whether the dependency is ignored when the migration scope is absent.
    /// </summary>
    public bool OptionalScope { get; }
}