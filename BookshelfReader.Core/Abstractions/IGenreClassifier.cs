namespace BookshelfReader.Core.Abstractions;

public interface IGenreClassifier
{
    IReadOnlyList<string> Classify(string title, string rawText);
}
