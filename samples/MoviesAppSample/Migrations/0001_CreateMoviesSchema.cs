using System.Data.SQLite;

namespace MoviesAppSample.Migrations;

internal sealed class CreateMoviesSchema_0001 : SQLiteDbMigration
{
    public override string Id => "0001_CreateMoviesSchema";

    public override void Up(SQLiteDbMigrationContext context)
    {
        context.ExecuteNonQuery(
            """
            CREATE TABLE Movies (
                Id INTEGER PRIMARY KEY,
                Title TEXT NOT NULL,
                Description TEXT,
                DateReleased INTEGER NOT NULL
            );

            CREATE TABLE Persons (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL UNIQUE
            );
            """);
    }
}