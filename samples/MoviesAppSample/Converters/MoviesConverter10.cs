using System.Data.SQLite;
using System.Diagnostics;

namespace MoviesAppSample.Converters;

internal sealed class MoviesConverter10: SQLiteDbConverter
{
    public override Version Version { get; } = new("1.0");

    protected override bool PerformUpdate(SQLiteDbConnection connection, CancellationToken cancellationToken)
    {
        if (!TryAddDbInfo(connection, Version.ToString(), cancellationToken))
        {
            Debug.Assert(false);
            return false;
        }
        CreateTableMovies(connection, cancellationToken);
        CreateTablePersons(connection, cancellationToken);
        return true;
    }

    private static void CreateTableMovies(SQLiteDbConnection connection, CancellationToken cancellationToken)
    {
        connection.ExecuteNonQuery("""
CREATE TABLE IF NOT EXISTS Movies (
    Id INTEGER PRIMARY KEY,
    Title TEXT NOT NULL,
    Description TEXT,
    DateReleased INTEGER NOT NULL);
""", cancellationToken: cancellationToken);
    }

    private static void CreateTablePersons(SQLiteDbConnection connection, CancellationToken cancellationToken)
    {
        connection.ExecuteNonQuery("""
CREATE TABLE IF NOT EXISTS Persons (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL UNIQUE);
""", cancellationToken: cancellationToken);
    }
}
