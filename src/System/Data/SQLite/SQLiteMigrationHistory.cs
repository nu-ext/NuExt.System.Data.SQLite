namespace System.Data.SQLite;

internal sealed class SQLiteMigrationHistory : IDbMigrationHistory<SQLiteDbMigrationContext>
{
    private const string MigrationHistoryTableName = "__nuext_migrations";

    private const string CreateMigrationHistoryTableCommand = $"""
CREATE TABLE IF NOT EXISTS {MigrationHistoryTableName} (
    sequence INTEGER PRIMARY KEY,
    migration_id TEXT NOT NULL UNIQUE CHECK(migration_id <> ''),
    version TEXT,
    applied_at INTEGER NOT NULL
    );
""";

    private const string GetAppliedMigrationsCommand = $"""
SELECT migration_id, applied_at, version
FROM {MigrationHistoryTableName}
ORDER BY sequence;
""";

    private const string AddAppliedMigrationCommand = $"""
INSERT INTO {MigrationHistoryTableName} (migration_id, version, applied_at)
VALUES (@migration_id, @version, @applied_at);
""";

    public void EnsureCreated(SQLiteDbMigrationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Connection.ExecuteNonQuery(CreateMigrationHistoryTableCommand, cancellationToken: context.CancellationToken);
    }

    public IReadOnlyList<AppliedDbMigration> GetAppliedMigrations(SQLiteDbMigrationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var migrations = new List<AppliedDbMigration>(8);
        context.Connection.ExecuteReader(GetAppliedMigrationsCommand, Read, cancellationToken: context.CancellationToken);
        return migrations;

        void Read(SQLiteDataReader reader)
        {
            context.ThrowIfCancellationRequested();
            migrations.Add(new AppliedDbMigration(reader.GetString(0), DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1)), reader.GetNullableString(2)));
        }
    }

    public void Add(SQLiteDbMigrationContext context, AppliedDbMigration migration)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(migration);

        context.Connection.ExecuteNonQuery(AddAppliedMigrationCommand, CommandBehavior.Default, context.CancellationToken,
            DbType.String.CreateInputParam("@migration_id", migration.Id),
            DbType.String.CreateInputParam("@version", migration.Version),
            DbType.Int64.CreateInputParam("@applied_at", migration.AppliedAt.ToUnixTimeMilliseconds()));
    }
}