namespace System.Data.SQLite;

/// <summary>
/// Represents a SQLite database migration that applies a single schema or data change.
/// </summary>
public abstract class SQLiteDbMigration : DbMigration<SQLiteDbMigrationContext>
{
}