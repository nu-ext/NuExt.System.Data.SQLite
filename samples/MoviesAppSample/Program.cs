using MoviesAppSample.Models;
using MoviesAppSample.Services;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace MoviesAppSample;

internal class Program
{
    private static async Task Main()
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            var moviesService = new MoviesService(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "movies.db"));
            await moviesService.InitializeAsync(cts.Token);

            //Sync and print movies
            await SyncDbMoviesWithJsonAsync(moviesService, cts.Token);
            await PrintDbMoviesAsync(moviesService, "Movies before editing", cts.Token);

            var dbMovies = await moviesService.GetAllMoviesAsync(cts.Token);
            var movie = dbMovies.FirstOrDefault(m => m.Title == "Terminator 2: Judgment Day");
            if (movie != null)
            {
                //Delete movie from db and print movies
                await moviesService.DeleteMovieAsync(movie.Id, cts.Token);
                await PrintDbMoviesAsync(moviesService, $"Movies after deleting '{movie.Title}'", cts.Token);

                //Modify and save movie to db
                movie.Id = -1;
                movie.DateReleased = DateTime.ParseExact("1991.07.03", "yyyy.MM.dd", CultureInfo.InvariantCulture, DateTimeStyles.None);
                movie.Starring.Add(new Person() { Name = "Edward Furlong" });
                movie.Starring.Add(new Person() { Name = "Earl Boen" });
                movie.Starring.Add(new Person() { Name = "Joe Morton" });
                var result = await moviesService.SaveMovieAsync(movie, cts.Token);
                Debug.Assert(result);

                //Print movies
                await PrintDbMoviesAsync(moviesService, $"Movies after updating '{movie.Title}'", cts.Token);
            }

            Console.ReadKey();
        }
        catch (OperationCanceledException ex)
        {
            Console.WriteLine(ex.ToString());
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(ex.ToString());
            Console.ReadKey();
        }

        void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("Ctrl+C pressed");
            e.Cancel = cts != null;
            cts?.Cancel();
        }
    }

    private static List<Movie> LoadMoviesFromJson(string filePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(filePath));
        JsonElement root = document.RootElement;
        var movies = new List<Movie>();
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement movieElement in root.EnumerateArray())
            {
                var title = movieElement.GetProperty("Title").GetString()!;
                string? description = null;
                if (movieElement.TryGetProperty("Description", out JsonElement descriptionElement))
                {
                    description = descriptionElement.GetString();
                }
                var dateReleased = DateTime.ParseExact(movieElement.GetProperty("DateReleased").GetString()!, "yyyy.MM.dd", CultureInfo.InvariantCulture, DateTimeStyles.None);
                var starring = new List<Person>();
                if (movieElement.TryGetProperty("Starring", out JsonElement starringElement) &&
                    starringElement.ValueKind == JsonValueKind.Array)
                {
                    starring.AddRange(starringElement.EnumerateArray().Select(element => new Person() { Name = element.GetString()! }));
                }
                var directedBy = new List<Person>();
                if (movieElement.TryGetProperty("Directed by", out JsonElement directedByElement) &&
                    directedByElement.ValueKind == JsonValueKind.Array)
                {
                    directedBy.AddRange(directedByElement.EnumerateArray().Select(element => new Person() { Name = element.GetString()! }));
                }
                var screenplayBy = new List<Person>();
                if (movieElement.TryGetProperty("Screenplay by", out JsonElement screenplayByElement) &&
                    screenplayByElement.ValueKind == JsonValueKind.Array)
                {
                    screenplayBy.AddRange(screenplayByElement.EnumerateArray().Select(element => new Person() { Name = element.GetString()! }));
                }
                var movie = new Movie()
                {
                    Title = title, Description = description, DateReleased = dateReleased,
                    Starring = starring, DirectedBy = directedBy, ScreenplayBy = screenplayBy
                };
                movies.Add(movie);
            }
        }
        return movies;
    }

    private static async ValueTask PrintDbMoviesAsync(MoviesService moviesService, string title, CancellationToken cancellationToken)
    {
        var dbMovies = await moviesService.GetAllMoviesAsync(cancellationToken);
        Console.WriteLine($"{title}: {dbMovies.Count}");

        int index = 0;
        foreach (var dbMovie in dbMovies)
        {
            Console.WriteLine($"{++index,-4}{dbMovie.Title} ({dbMovie.DateReleased.Date:D})");
            if (!string.IsNullOrEmpty(dbMovie.Description))
            {
                Console.WriteLine(dbMovie.Description);
            }
            if (!dbMovie.Starring.IsNullOrEmpty())
            {
                Console.WriteLine($"\tStarring:");
                foreach (var person in dbMovie.Starring)
                {
                    Console.WriteLine($"\t\t{person.Name}");
                }
            }
            if (!dbMovie.DirectedBy.IsNullOrEmpty())
            {
                Console.WriteLine($"\tDirected by:");
                foreach (var person in dbMovie.DirectedBy)
                {
                    Console.WriteLine($"\t\t{person.Name}");
                }
            }
            if (!dbMovie.DirectedBy.IsNullOrEmpty())
            {
                Console.WriteLine($"\tScreenplay by:");
                foreach (var person in dbMovie.ScreenplayBy)
                {
                    Console.WriteLine($"\t\t{person.Name}");
                }
            }
            Console.WriteLine();
        }
    }

    private static async ValueTask SyncDbMoviesWithJsonAsync(MoviesService moviesService, CancellationToken cancellationToken)
    {
        var dbMovies = await moviesService.GetAllMoviesAsync(cancellationToken);
        var jsonMovies = LoadMoviesFromJson(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "movies.json"));

        foreach (var movie in jsonMovies)
        {
            var dbMovie = dbMovies.FirstOrDefault(dto => dto.Title == movie.Title);
            if (dbMovie != null)
            {
                movie.Id = dbMovie.Id;
            }
            bool result = await moviesService.SaveMovieAsync(movie, cancellationToken);
            Debug.Assert(result);
        }

        var moviesToDelete = dbMovies.Where(dto => jsonMovies.Any(m => m.Title == dto.Title) == false).ToList();
        if (!moviesToDelete.IsNullOrEmpty())
        {
            bool result = await moviesService.DeleteMoviesAsync(moviesToDelete, cancellationToken);
            Debug.Assert(result);
        }
    }
}
