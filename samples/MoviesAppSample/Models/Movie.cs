using System.Diagnostics;

namespace MoviesAppSample.Models;

[DebuggerDisplay("Id={Id}, Title={Title}, DateReleased={DateReleased}")]
public class Movie
{
    public long Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required DateTime DateReleased { get; set; }
    public required List<Person> Starring { get; set; }
    public required List<Person> DirectedBy { get; set; }
    public required List<Person> ScreenplayBy { get; set; }
}

public static class MovieExtensions
{
    public static MovieDto ToDto(this Movie movie)
    {
        ArgumentNullException.ThrowIfNull(movie);

        return new MovieDto(movie.Id, movie.Title, movie.Description, movie.DateReleased);
    }
}
