using System.Data;
using System.Data.SQLite;
using System.Diagnostics;

namespace MoviesAppSample.DataAccess;

internal sealed class PersonDal(Func<SQLiteDbConnection> createConnection) : SQLiteDalBase(createConnection)
{
    private static readonly string[] s_columns = ["Id", "Name"];

    private static readonly string[] s_idColumns = ["Id"];

    private static readonly string[] s_updateColumns = ["Name"];

    private static readonly string[] s_relationshipColumns = ["MovieId", "PersonId"];

    private static readonly string[] s_relationshipTables = ["MovieCasts", "MovieDirectors", "MovieWriters"];

    #region Properties

    protected override string[] Columns => s_columns;
    protected override string TableName => "Persons";

    #endregion

    #region Methods

    public ValueTask<bool> DeleteMoviePersonsAsync(SQLiteDbContext context, long movieId, CancellationToken cancellationToken)
    {
        Debug.Assert(movieId > 0);
        cancellationToken.ThrowIfCancellationRequested();
        return TryExecuteInDbContextAsync(context, ctx =>
        {
            int affected = 0;
            foreach (var tableName in s_relationshipTables)
            {
                using var command = ctx.Connection.CreateCommandDelete(tableName, "WHERE MovieId=@MovieId",
                    DbType.Int64.CreateInputParam("@MovieId", movieId));
                affected += ctx.Connection.ExecuteNonQuery(command, cancellationToken: cancellationToken);
            }
            return affected > 0;
        }, cancellationToken);
    }

    public ValueTask<List<PersonDto>> LoadMovieCastsAsync(SQLiteDbContext context, long movieId,
        CancellationToken cancellationToken)
    {
        return LoadMoviePersonsAsync(context, "MovieCasts", movieId, cancellationToken);
    }

    public ValueTask<List<PersonDto>> LoadMovieDirectorsAsync(SQLiteDbContext context, long movieId,
        CancellationToken cancellationToken)
    {
        return LoadMoviePersonsAsync(context, "MovieDirectors", movieId, cancellationToken);
    }

    public ValueTask<List<PersonDto>> LoadMovieWritersAsync(SQLiteDbContext context, long movieId,
        CancellationToken cancellationToken)
    {
        return LoadMoviePersonsAsync(context, "MovieWriters", movieId, cancellationToken);
    }

    private ValueTask<List<PersonDto>> LoadMoviePersonsAsync(SQLiteDbContext context, string tableName, long movieId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return TryExecuteInDbContextAsync(context, ctx =>
        {
            using var command = ctx.Connection.CreateCommand($"""
                SELECT p.Id, p.Name
                FROM {tableName} mp
                JOIN Persons p ON mp.PersonId = p.Id
                WHERE mp.MovieId = @MovieId;
                """, DbType.Int64.CreateInputParam("@MovieId", movieId));
            var list = new List<PersonDto>();
            void Read(SQLiteDataReader reader)
            {
                list.Add(new PersonDto(
                    reader.GetInt64(0),
                    reader.GetString(1)
                ));
            }
            int count = ctx.Connection.ExecuteReader(command, Read, cancellationToken: cancellationToken);
            Debug.Assert(list.Count == count);
            return list;
        }, cancellationToken);
    }

    public ValueTask<bool> SaveMovieCastsAsync(SQLiteDbContext context, long movieId, List<PersonDto> dtos,
        CancellationToken cancellationToken)
    {
        return SaveMoviePersonsAsync(context, "MovieCasts", movieId, dtos, cancellationToken);
    }

    public ValueTask<bool> SaveMovieDirectorsAsync(SQLiteDbContext context, long movieId, List<PersonDto> dtos,
        CancellationToken cancellationToken)
    {
        return SaveMoviePersonsAsync(context, "MovieDirectors", movieId, dtos, cancellationToken);
    }

    public ValueTask<bool> SaveMovieWritersAsync(SQLiteDbContext context, long movieId, List<PersonDto> dtos,
        CancellationToken cancellationToken)
    {
        return SaveMoviePersonsAsync(context, "MovieWriters", movieId, dtos, cancellationToken);
    }

    private ValueTask<bool> SaveMoviePersonsAsync(SQLiteDbContext context, string tableName, long movieId, List<PersonDto> dtos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dtos);
        Debug.Assert(movieId > 0);

        cancellationToken.ThrowIfCancellationRequested();
        return TryExecuteInDbContextAsync(context, ctx =>
        {
            var (adapter, table) = ctx.Connection.SelectForUpdate(tableName, s_relationshipColumns, "WHERE MovieId=@MovieId", true, cancellationToken,
                DbType.Int64.CreateInputParam("@MovieId", movieId));
            //table.PrimaryKey = new[] { table.Columns["MovieId"], table.Columns["PersonId"] };
            using (adapter)
            {
                var processedRows = new HashSet<DataRow>();
                foreach (var dto in dtos)
                {
                    Debug.Assert(dto.Id > 0);
                    var row = table.FindRow("PersonId", o => Convert.ToInt64(o) == dto.Id);
                    if (row == null)
                    {
                        row = table.NewRow();
                        row["MovieId"] = movieId;
                        row["PersonId"] = dto.Id;
                        table.Rows.Add(row);
                    }
                    processedRows.Add(row);
                }
                var rowsToDelete = table.Rows.Cast<DataRow>().Except(processedRows);
                foreach (var row in rowsToDelete)
                {
                    row.Delete();
                }
                int num = 0;
                if (table.HasChanges())
                {
                    num = ctx.Connection.Update(adapter, table, cancellationToken);
                }
                table.ClearAndDispose();
                return num > 0;
            }
        }, cancellationToken);
    }

    public ValueTask<bool> SavePersonsAsync(SQLiteDbContext context, List<PersonDto> dtos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dtos);

        cancellationToken.ThrowIfCancellationRequested();
        return TryExecuteInDbContextAsync(context, ctx =>
        {
            using var commandInsert = ctx.Connection.CreateCommandInsertOrIgnore(TableName, s_updateColumns,
                "RETURNING Id",
                DbType.String.CreateInputParam("@Name", string.Empty));
            using var commandSelect = ctx.Connection.CreateCommandSelect(TableName, s_idColumns,
                "WHERE Name=@Name",
                DbType.String.CreateInputParam("@Name", string.Empty));
            int affected = 0;
            for (int i = 0; i < dtos.Count; i++)
            {
                var dto = dtos[i];
                commandInsert.Parameters["@Name"].Value = dto.Name;
                var id = ctx.Connection.ExecuteScalar(commandInsert, cancellationToken: cancellationToken);
                if (id is null)//person alreasy exists
                {
                    commandSelect.Parameters["@Name"].Value = dto.Name;
                    id = ctx.Connection.ExecuteScalar(commandSelect, cancellationToken: cancellationToken);
                    Debug.Assert(id is not null);
                }
                Debug.Assert(id is long);
                dtos[i] = dto with { Id = Convert.ToInt64(id) };
                Debug.Assert(dtos[i].Id > 0);
                affected++;
            }
            return affected > 0;
        }, cancellationToken);
    }

    #endregion
}
