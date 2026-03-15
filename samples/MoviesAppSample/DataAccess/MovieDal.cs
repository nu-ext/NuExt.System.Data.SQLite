using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;

namespace MoviesAppSample.DataAccess;

internal sealed class MovieDal(Func<SQLiteDbConnection> createConnection) : SQLiteDalBase(createConnection)
{
    private static readonly string[] s_columns = ["Id", "Title", "Description", "DateReleased"];

    private static readonly string[] s_updateColumns = ["Title", "Description", "DateReleased"];

    #region Properties

    protected override string[] Columns => s_columns;

    protected override string TableName => "Movies";

    #endregion

    #region Methods

    public ValueTask<bool> DeleteMovieAsync(SQLiteDbContext? context, long id, CancellationToken cancellationToken)
    {
        Debug.Assert(id > 0);
        cancellationToken.ThrowIfCancellationRequested();
        return TryExecuteInDbContextAsync(context, async ctx =>
        {
            using var personDal = new PersonDal(CreateConnection);
            bool result = await personDal.DeleteMoviePersonsAsync(ctx, id, cancellationToken);

#if NET6_0_OR_GREATER

            await
#endif

                using var command = ctx.Connection.CreateCommandDelete(TableName, "WHERE Id=@Id",
                DbType.Int64.CreateInputParam("@Id", id));
            int affected = ctx.Connection.ExecuteNonQuery(command, cancellationToken: cancellationToken);
            return affected == 1;
        }, cancellationToken);
    }

    public ValueTask<bool> DeleteMoviesAsync(SQLiteDbContext? context, IEnumerable<long> ids, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return TryExecuteInDbContextAsync(context, async ctx =>
        {
            using var personDal = new PersonDal(CreateConnection);

#if NET6_0_OR_GREATER

            await
#endif

                using var command = ctx.Connection.CreateCommandDelete(TableName, "WHERE Id=@Id",
                    DbType.Int64.CreateInputParam("@Id", 0));
            int affected = 0;
            foreach (var id in ids)
            {
                bool result = await personDal.DeleteMoviePersonsAsync(ctx, id, cancellationToken);
                command.Parameters["@Id"].Value = id;
                affected += ctx.Connection.ExecuteNonQuery(command, cancellationToken: cancellationToken);
            }
            return affected > 0;
        }, cancellationToken);
    }

    public ValueTask<MovieDto?> LoadMovieAsync(SQLiteDbContext? context, long id, CancellationToken cancellationToken)
    {
        Debug.Assert(id > 0);
        cancellationToken.ThrowIfCancellationRequested();
        return TryExecuteInDbContextAsync(context, ctx =>
        {
            using var command = ctx.Connection.CreateCommandSelect(TableName, Columns, "WHERE Id=@Id LIMIT 1",
                DbType.Int64.CreateInputParam("@Id", id));
            MovieDto? dto = null;
            int count = ctx.Connection.ExecuteReader(command, Read, cancellationToken: cancellationToken);
            Debug.Assert(1 == count);
            return dto;

            void Read(SQLiteDataReader reader)
            {
                dto = ReadMovie(reader);
            }
        }, cancellationToken);
    }

    public ValueTask<List<MovieDto>> LoadMoviesAsync(SQLiteDbContext? context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return TryExecuteInDbContextAsync(context, ctx =>
        {
            using var command = ctx.Connection.CreateCommandSelect(TableName, Columns, "ORDER BY DateReleased");
            var list = new List<MovieDto>();
            int count = ctx.Connection.ExecuteReader(command, Read, cancellationToken: cancellationToken);
            Debug.Assert(list.Count == count);
            return list;

            void Read(SQLiteDataReader reader)
            {
                list.Add(ReadMovie(reader));
            }
        }, cancellationToken);
    }

    private static MovieDto ReadMovie(DbDataReader reader)
    {
        return new MovieDto(
            reader.GetInt64("Id"),
            reader.GetString("Title"),
            reader.GetNullableString("Description"),
            reader.GetDateTime("DateReleased")
        );
    }

    public ValueTask<(bool result, MovieDto dto)> SaveMovieAsync(SQLiteDbContext? context, MovieDto dto, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return TryExecuteInDbContextAsync(context, ctx =>
        {
            SQLiteCommand command;
            if (dto.Id <= 0)
            {
                command = ctx.Connection.CreateCommandInsert(TableName, s_updateColumns,
                    parameters: [
                    DbType.String.CreateInputParam("@Title", dto.Title),
                    DbType.String.CreateInputParam("@Description", dto.Description),
                    DbType.DateTime.CreateInputParam("@DateReleased", dto.DateReleased) ]);
            }
            else
            {
                command = ctx.Connection.CreateCommandUpdate(TableName, s_updateColumns, "WHERE Id = @Id",
                    DbType.String.CreateInputParam("@Title", dto.Title),
                    DbType.String.CreateInputParam("@Description", dto.Description),
                    DbType.DateTime.CreateInputParam("@DateReleased", dto.DateReleased),
                    DbType.Int64.CreateInputParam("@Id", dto.Id));
            }
            using (command)
            {
                int affected = ctx.Connection.ExecuteNonQuery(command, cancellationToken: cancellationToken);
                Debug.Assert(affected == 1);
            }
            if (dto.Id <= 0)
            {
                dto = dto with { Id = ctx.Connection.LastInsertRowId };
            }
            return (true, dto);
        }, cancellationToken);
    }

    #endregion
}
