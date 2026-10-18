namespace System.Data.SQLite;

internal sealed class SQLiteMigrationHistorySnapshot
{
    private readonly Dictionary<string, List<AppliedDbMigration>> _migrationsByScope = new(StringComparer.Ordinal);

    private readonly HashSet<(string Scope, string MigrationId)> _appliedMigrations = [];

    internal bool MigrationScopeExists(string scope)
    {
        return _migrationsByScope.ContainsKey(scope);
    }

    internal bool IsMigrationApplied(string scope, string migrationId)
    {
        return _appliedMigrations.Contains((scope, migrationId));
    }

    internal IReadOnlyList<AppliedDbMigration> GetAppliedMigrations(string scope)
    {
        return _migrationsByScope.TryGetValue(scope, out var migrations) ? migrations : [];
    }

    internal void Add(string scope, AppliedDbMigration migration)
    {
        if (!_migrationsByScope.TryGetValue(scope, out var migrations))
        {
            migrations = [migration];
            _migrationsByScope.Add(scope, migrations);
        }
        else
        {
            migrations.Add(migration);
        }

        if (!_appliedMigrations.Add((scope, migration.Id)))
        {
            throw new DbMigrationException($"Migration '{scope}/{migration.Id}' is recorded more than once.");
        }
    }
}