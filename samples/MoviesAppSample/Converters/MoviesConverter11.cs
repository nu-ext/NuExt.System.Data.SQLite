using System.Data.SQLite;

namespace MoviesAppSample.Converters;

internal sealed class MoviesConverter11 : SQLiteDbConverter
{
    public override Version Version { get; } = new("1.1");

    protected override bool PerformUpdate(SQLiteDbConnection connection, CancellationToken cancellationToken)
    {
        CreateTableMovieCasts(connection, cancellationToken);
        CreateTableMovieDirectors(connection, cancellationToken);
        CreateTableMovieWriters(connection, cancellationToken);
        return true;
    }

    private static void CreateTableMovieCasts(SQLiteDbConnection connection, CancellationToken cancellationToken)
    {
        connection.ExecuteNonQuery("""
CREATE TABLE IF NOT EXISTS MovieCasts (
    MovieId INTEGER NOT NULL,
    PersonId INTEGER NOT NULL,
    FOREIGN KEY (MovieId) REFERENCES Movies(Id),
    FOREIGN KEY (PersonId) REFERENCES Persons(Id),
    PRIMARY KEY (MovieId, PersonId)
);
""", cancellationToken: cancellationToken);
    }

    private static void CreateTableMovieDirectors(SQLiteDbConnection connection, CancellationToken cancellationToken)
    {
        connection.ExecuteNonQuery("""
CREATE TABLE IF NOT EXISTS MovieDirectors (
    MovieId INTEGER NOT NULL,
    PersonId INTEGER NOT NULL,
    FOREIGN KEY (MovieId) REFERENCES Movies(Id),
    FOREIGN KEY (PersonId) REFERENCES Persons(Id),
    PRIMARY KEY (MovieId, PersonId)
);
""", cancellationToken : cancellationToken);
    }

    private static void CreateTableMovieWriters(SQLiteDbConnection connection, CancellationToken cancellationToken)
    {
        connection.ExecuteNonQuery("""
CREATE TABLE IF NOT EXISTS MovieWriters (
    MovieId INTEGER NOT NULL,
    PersonId INTEGER NOT NULL,
    FOREIGN KEY (MovieId) REFERENCES Movies(Id),
    FOREIGN KEY (PersonId) REFERENCES Persons(Id),
    PRIMARY KEY (MovieId, PersonId)
);
""", cancellationToken: cancellationToken);
    }
}
