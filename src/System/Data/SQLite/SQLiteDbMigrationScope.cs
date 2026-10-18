namespace System.Data.SQLite;

internal sealed record SQLiteDbMigrationScope
{
    internal SQLiteDbMigrationScope(string name, SQLiteDbMigration[] migrations, string? version)
    {
        Name = name;
        Migrations = migrations;
        Version = version;
        History = new SQLiteMigrationHistory(name);
    }

    internal string Name { get; }

    internal SQLiteDbMigration[] Migrations { get; }

    internal string? Version { get; }

    internal SQLiteMigrationHistory History { get; }
}

internal static class SQLiteDbMigrationScopeExtensions
{
    internal static bool ContainsMigration(this SQLiteDbMigrationScope scope, string migrationId)
    {
        for (var i = 0; i < scope.Migrations.Length; i++)
        {
            if (StringComparer.Ordinal.Equals(scope.Migrations[i].Id, migrationId))
            {
                return true;
            }
        }
        return false;
    }

    internal static SQLiteDbMigrationScope? FindScope(this IReadOnlyList<SQLiteDbMigrationScope> scopes, string scope)
    {
        for (var i = 0; i < scopes.Count; i++)
        {
            var item = scopes[i];
            if (StringComparer.Ordinal.Equals(item.Name, scope))
            {
                return item;
            }
        }
        return null;
    }

    internal static bool HasPendingMigrations(this IReadOnlyList<SQLiteDbMigrationScope> scopes, ReadOnlySpan<int> nextMigrationIndexes)
    {
        for (var i = 0; i < nextMigrationIndexes.Length; i++)
        {
            if (nextMigrationIndexes[i] < scopes[i].Migrations.Length)
            {
                return true;
            }
        }
        return false;
    }

    internal static bool IsScopeRegistered(this IReadOnlyList<SQLiteDbMigrationScope> scopes, string scope)
    {
        for (var i = 0; i < scopes.Count; i++)
        {
            if (StringComparer.Ordinal.Equals(scopes[i].Name, scope))
            {
                return true;
            }
        }
        return false;
    }
}