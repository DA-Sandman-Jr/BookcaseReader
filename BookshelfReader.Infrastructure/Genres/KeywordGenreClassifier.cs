using BookshelfReader.Core.Abstractions;

namespace BookshelfReader.Infrastructure.Genres;

public sealed class KeywordGenreClassifier : IGenreClassifier
{
    private static readonly Dictionary<string, string> KeywordToGenre = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dragon"] = "Fantasy",
        ["wizard"] = "Fantasy",
        ["spaceship"] = "Science Fiction",
        ["galaxy"] = "Science Fiction",
        ["murder"] = "Mystery",
        ["detective"] = "Mystery",
        ["love"] = "Romance",
        ["recipe"] = "Cooking",
        ["history"] = "History",
        ["biography"] = "Biography"
    };

    public IReadOnlyList<string> Classify(string title, string rawText)
    {
        var genres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AnalyzeText(title, genres);
        AnalyzeText(rawText, genres);
        return genres.Count == 0 ? Array.Empty<string>() : genres.ToArray();
    }

    private static void AnalyzeText(string text, HashSet<string> genres)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (var (keyword, genre) in KeywordToGenre)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                genres.Add(genre);
            }
        }
    }
}
