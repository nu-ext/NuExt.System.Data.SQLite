namespace System.Data.SQLite;

public sealed class SQLiteDbMigrationContext : DbMigrationContext
{
    internal SQLiteDbMigrationContext(SQLiteDbConnection connection, CancellationToken cancellationToken) : base(cancellationToken)
    {
        Connection = connection;
    }

    internal SQLiteDbConnection Connection { get; }

    public int ExecuteNonQuery(string commandText)
    {
        return Connection.ExecuteNonQuery(commandText, cancellationToken: CancellationToken);
    }
}
