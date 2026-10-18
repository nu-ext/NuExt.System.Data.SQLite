namespace System.Data.SQLite;

internal sealed class SQLiteMigrationHistory : IDbMigrationHistory<SQLiteDbMigrationContext>
{
    internal const string DefaultScope = "default";

    private const string MigrationHistoryTableName = "__nuext_migrations";

    private const string CreateMigrationHistoryTableCommand = $"""
CREATE TABLE IF NOT EXISTS {MigrationHistoryTableName} (
    id INTEGER PRIMARY KEY,
    scope TEXT NOT NULL CHECK(scope <> ''),
    sequence INTEGER NOT NULL CHECK(sequence > 0),
    migration_id TEXT NOT NULL CHECK(migration_id <> ''),
    dependencies TEXT,
    version TEXT,
    applied_at INTEGER NOT NULL,
    UNIQUE(scope, sequence),
    UNIQUE(scope, migration_id)
);
""";

    private const string GetAppliedMigrationsCommand = $"""
SELECT sequence, migration_id, applied_at, version
FROM {MigrationHistoryTableName}
WHERE scope = @scope
ORDER BY sequence;
""";

    private const string AddAppliedMigrationCommand = $"""
INSERT INTO {MigrationHistoryTableName} (
    scope,
    sequence,
    migration_id,
    dependencies,
    version,
    applied_at
)
VALUES (
    @scope,
    @sequence,
    @migration_id,
    @dependencies,
    @version,
    @applied_at
);
""";

    private const string MigrationScopeExistsCommand = $"""
SELECT EXISTS (
    SELECT 1
    FROM {MigrationHistoryTableName}
    WHERE scope = @scope
);
""";

    private const string IsMigrationAppliedCommand = $"""
SELECT EXISTS (
    SELECT 1
    FROM {MigrationHistoryTableName}
    WHERE scope = @scope AND migration_id = @migration_id
);
""";

    private const string GetMigrationHistoryCommand = $"""
SELECT scope, sequence, migration_id, applied_at, version
FROM {MigrationHistoryTableName}
ORDER BY id;
""";

    private readonly string _scope;

    internal SQLiteMigrationHistory(string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        _scope = scope;
    }

    /// <inheritdoc/>
    public void EnsureCreated(SQLiteDbMigrationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Connection.ExecuteNonQuery(CreateMigrationHistoryTableCommand, cancellationToken: context.CancellationToken);
    }

    /// <inheritdoc/>
    public IReadOnlyList<AppliedDbMigration> GetAppliedMigrations(SQLiteDbMigrationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var migrations = new List<AppliedDbMigration>(8);
        context.Connection.ExecuteReader(GetAppliedMigrationsCommand, Read, migrations, context.CancellationToken,
            DbType.String.CreateInputParam("@scope", _scope));
        return migrations;

        static void Read(SQLiteDataReader reader, List<AppliedDbMigration> migrations)
        {
            migrations.Add(new AppliedDbMigration(reader.GetInt32(0), reader.GetString(1), DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(2)), reader.GetNullableString(3)));
        }
    }

    internal static SQLiteMigrationHistorySnapshot GetMigrationHistory(SQLiteDbMigrationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var history = new SQLiteMigrationHistorySnapshot();
        context.Connection.ExecuteReader(GetMigrationHistoryCommand, Read, history, context.CancellationToken);
        return history;

        static void Read(SQLiteDataReader reader, SQLiteMigrationHistorySnapshot history)
        {
            var scope = reader.GetString(0);
            history.Add(scope, new AppliedDbMigration(reader.GetInt32(1), reader.GetString(2), DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(3)), reader.GetNullableString(4)));
        }
    }

    /// <inheritdoc/>
    public void Add(SQLiteDbMigrationContext context, AppliedDbMigration migration)
    {
        Add(context, migration, null);
    }

    internal void Add(SQLiteDbMigrationContext context, AppliedDbMigration migration, string? dependencies)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(migration);

        context.Connection.ExecuteNonQuery(AddAppliedMigrationCommand, context.CancellationToken,
            DbType.String.CreateInputParam("@scope", _scope),
            DbType.Int32.CreateInputParam("@sequence", migration.Sequence),
            DbType.String.CreateInputParam("@migration_id", migration.Id),
            DbType.String.CreateInputParam("@dependencies", dependencies),
            DbType.String.CreateInputParam("@version", migration.Version),
            DbType.Int64.CreateInputParam("@applied_at", migration.AppliedAt.ToUnixTimeMilliseconds()));
    }

    internal static bool MigrationScopeExists(SQLiteDbMigrationContext context, string scope)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var result = context.Connection.ExecuteScalar(MigrationScopeExistsCommand, context.CancellationToken,
            DbType.String.CreateInputParam("@scope", scope));
        return Convert.ToInt64(result) != 0;
    }

    internal static bool IsMigrationApplied(SQLiteDbMigrationContext context, string scope, string migrationId)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationId);

        var result = context.Connection.ExecuteScalar(IsMigrationAppliedCommand, context.CancellationToken,
            DbType.String.CreateInputParam("@scope", scope),
            DbType.String.CreateInputParam("@migration_id", migrationId));
        return Convert.ToInt64(result) != 0;
    }
}