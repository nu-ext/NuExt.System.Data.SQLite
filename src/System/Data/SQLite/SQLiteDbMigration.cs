namespace System.Data.SQLite;

/// <summary>
/// Represents a SQLite database migration that applies a single schema or data change.
/// </summary>
public abstract class SQLiteDbMigration : DbMigration<SQLiteDbMigrationContext>
{
    /// <summary>
    /// Gets the migrations that must be applied before this migration.
    /// </summary>
    public virtual IReadOnlyList<SQLiteDbMigrationDependency> Dependencies => [];
}