using MoviesAppSample.Converters;
using MoviesAppSample.DataAccess;
using MoviesAppSample.Models;
using System.Data;
using System.Data.SQLite;

namespace MoviesAppSample.Services;

internal class MoviesService(string dataBasePath)
{
    private static readonly SQLiteDbConverter[] s_moviesConverters = [new MoviesConverter10(), new MoviesConverter11()
    ];

    #region Properties

    public string DataBasePath { get; } = dataBasePath ?? throw new ArgumentNullException(nameof(dataBasePath));

    #endregion

    #region Methods

    private SQLiteDbConnection CreateConnection() => new(GetMoviesConnectionString());

    public async ValueTask<bool> DeleteMovieAsync(long movieId, CancellationToken cancellationToken)
    {
        using var movieDal = new MovieDal(CreateConnection);
        return await movieDal.DeleteMovieAsync(null, movieId, cancellationToken);
    }

    public async ValueTask<bool> DeleteMoviesAsync(List<Movie> moviesToDelete, CancellationToken cancellationToken)
    {
        using var movieDal = new MovieDal(CreateConnection);
        return await movieDal.DeleteMoviesAsync(null, moviesToDelete.Select(m => m.Id), cancellationToken);
    }

    public async ValueTask<List<Movie>> GetAllMoviesAsync(CancellationToken cancellationToken)
    {
        using var connection = CreateConnection();

        return await connection.AcquireLockAsync(async () =>
        {
            using var dbContext = new SQLiteDbContext(connection);
            try
            {
                s_moviesConverters.Initialize(dbContext, cancellationToken);
                using var movieDal = new MovieDal(CreateConnection);
                using var personDal = new PersonDal(CreateConnection);
                var movieDtos = await movieDal.LoadMoviesAsync(dbContext, cancellationToken);
                var movies = new List<Movie>(movieDtos.Count);
                foreach (var dto in movieDtos)
                {
                    var starringDtos = await personDal.LoadMovieCastsAsync(dbContext, dto.Id, cancellationToken);
                    var directedByDtos = await personDal.LoadMovieDirectorsAsync(dbContext, dto.Id, cancellationToken);
                    var screenplayByDtos = await personDal.LoadMovieWritersAsync(dbContext, dto.Id, cancellationToken);
                    var movie = new Movie() 
                    { 
                        Id = dto.Id, Title = dto.Title, Description = dto.Description, DateReleased = dto.DateReleased, 
                        Starring = [.. starringDtos.Select(p => new Person(){ Id = p.Id, Name = p.Name})], 
                        DirectedBy = [.. directedByDtos.Select(p => new Person() { Id = p.Id, Name = p.Name })],
                        ScreenplayBy = [.. screenplayByDtos.Select(p => new Person() { Id = p.Id, Name = p.Name })]
                    };
                    movies.Add(movie);
                }
                dbContext.Commit();
                return movies;
            }
            catch
            {
                dbContext.Rollback();
                throw;
            }
        }, cancellationToken);
    }

    private string GetMoviesConnectionString()
    {
        var csb = new SQLiteConnectionStringBuilder
        {
            DataSource = DataBasePath,
            DateTimeFormat = SQLiteDateFormats.UnixEpoch,
            DateTimeKind = DateTimeKind.Utc,
            ForeignKeys = true
        };
        return csb.ConnectionString;
    }

    public ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        s_moviesConverters.Initialize(CreateConnection, cancellationToken);
        return default;
    }

    public async ValueTask<bool> SaveMovieAsync(Movie movie, CancellationToken cancellationToken)
    {
        using var connection = CreateConnection();

        return await connection.AcquireLockAsync(async () =>
        {
            using var dbContext = new SQLiteDbContext(connection);
            try
            {
                s_moviesConverters.Initialize(dbContext, cancellationToken);
                using var movieDal = new MovieDal(CreateConnection);
                using var personDal = new PersonDal(CreateConnection);

                #region Save Movie
                var (result, movieDto) = await movieDal.SaveMovieAsync(dbContext, movie.ToDto(), cancellationToken);
                if (!result)
                {
                    return false;
                }
                movie.Id = movieDto.Id;
                #endregion

                #region Save Starring
                var starringDtos = movie.Starring.Select(s => s.ToDto()).ToList();
                result = await personDal.SavePersonsAsync(dbContext, starringDtos, cancellationToken);
                if (result)
                {
                    for (int i = 0; i < starringDtos.Count; i++)
                    {
                        movie.Starring[i].Id = starringDtos[i].Id;
                    }
                    result = await personDal.SaveMovieCastsAsync(dbContext, movie.Id, starringDtos, cancellationToken);
                }
                #endregion

                #region Save Directed by
                var directedByDtos = movie.DirectedBy.Select(s => s.ToDto()).ToList();
                result = await personDal.SavePersonsAsync(dbContext, directedByDtos, cancellationToken);
                if (result)
                {
                    for (int i = 0; i < directedByDtos.Count; i++)
                    {
                        movie.DirectedBy[i].Id = directedByDtos[i].Id;
                    }
                    result = await personDal.SaveMovieDirectorsAsync(dbContext, movie.Id, directedByDtos, cancellationToken);
                }
                #endregion

                #region Save Screenplay by
                var screenplayByDtos = movie.ScreenplayBy.Select(s => s.ToDto()).ToList();
                result = await personDal.SavePersonsAsync(dbContext, screenplayByDtos, cancellationToken);
                if (result)
                {
                    for (int i = 0; i < screenplayByDtos.Count; i++)
                    {
                        movie.ScreenplayBy[i].Id = screenplayByDtos[i].Id;
                    }
                    result = await personDal.SaveMovieWritersAsync(dbContext, movie.Id, screenplayByDtos, cancellationToken);
                }
                #endregion

                dbContext.Commit();
                return true;
            }
            catch
            {
                dbContext.Rollback();
                throw;
            }
        }, cancellationToken);
    }

    #endregion
}
