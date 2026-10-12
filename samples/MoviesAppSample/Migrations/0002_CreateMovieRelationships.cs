using System.Data.SQLite;

namespace MoviesAppSample.Migrations;

internal sealed class CreateMovieRelationships_0002 : SQLiteDbMigration
{
    public override string Id => "0002_CreateMovieRelationships";

    public override void Up(SQLiteDbMigrationContext context)
    {
        context.ExecuteNonQuery(
            """
            CREATE TABLE MovieCasts (
                MovieId INTEGER NOT NULL,
                PersonId INTEGER NOT NULL,
                FOREIGN KEY (MovieId) REFERENCES Movies(Id),
                FOREIGN KEY (PersonId) REFERENCES Persons(Id),
                PRIMARY KEY (MovieId, PersonId)
            );

            CREATE TABLE MovieDirectors (
                MovieId INTEGER NOT NULL,
                PersonId INTEGER NOT NULL,
                FOREIGN KEY (MovieId) REFERENCES Movies(Id),
                FOREIGN KEY (PersonId) REFERENCES Persons(Id),
                PRIMARY KEY (MovieId, PersonId)
            );

            CREATE TABLE MovieWriters (
                MovieId INTEGER NOT NULL,
                PersonId INTEGER NOT NULL,
                FOREIGN KEY (MovieId) REFERENCES Movies(Id),
                FOREIGN KEY (PersonId) REFERENCES Persons(Id),
                PRIMARY KEY (MovieId, PersonId)
            );
            """);
    }
}