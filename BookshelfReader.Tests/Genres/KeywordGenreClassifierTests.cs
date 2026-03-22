using BookshelfReader.Infrastructure.Genres;
using FluentAssertions;
using Xunit;

namespace BookshelfReader.Tests.Genres;

public class KeywordGenreClassifierTests
{
    private readonly KeywordGenreClassifier _classifier = new();

    [Fact]
    public void Classify_ReturnsEmpty_WhenNoKeywordsMatch()
    {
        var result = _classifier.Classify("The Old Man and the Sea", "The Old Man and the Sea");

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("dragon", "Fantasy")]
    [InlineData("wizard", "Fantasy")]
    [InlineData("spaceship", "Science Fiction")]
    [InlineData("galaxy", "Science Fiction")]
    [InlineData("murder", "Mystery")]
    [InlineData("detective", "Mystery")]
    [InlineData("love", "Romance")]
    [InlineData("recipe", "Cooking")]
    [InlineData("history", "History")]
    [InlineData("biography", "Biography")]
    public void Classify_MatchesKeyword_WhenExactWordPresent(string keyword, string expectedGenre)
    {
        var result = _classifier.Classify(keyword, string.Empty);

        result.Should().ContainSingle().Which.Should().Be(expectedGenre);
    }

    [Fact]
    public void Classify_IsCaseInsensitive()
    {
        var result = _classifier.Classify("DRAGON", string.Empty);

        result.Should().ContainSingle().Which.Should().Be("Fantasy");
    }

    [Fact]
    public void Classify_DoesNotMatchSubstrings()
    {
        // "love" inside "beloved" should not trigger Romance
        var result = _classifier.Classify("The Beloved", "beloved gloves loveable");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Classify_MatchesKeywordInRawText_WhenNotInTitle()
    {
        var result = _classifier.Classify("A Dark Night", "The detective investigated the scene");

        result.Should().ContainSingle().Which.Should().Be("Mystery");
    }

    [Fact]
    public void Classify_ReturnsMultipleGenres_WhenMultipleKeywordsPresent()
    {
        var result = _classifier.Classify("Love and Murder", string.Empty);

        result.Should().HaveCount(2)
            .And.Contain("Romance")
            .And.Contain("Mystery");
    }

    [Fact]
    public void Classify_ReturnsEmpty_WhenBothInputsAreEmpty()
    {
        var result = _classifier.Classify(string.Empty, string.Empty);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Classify_ReturnsEmpty_WhenBothInputsAreWhitespace()
    {
        var result = _classifier.Classify("   ", "\t\n");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Classify_DoesNotDuplicateGenres_WhenKeywordAppearsMultipleTimes()
    {
        var result = _classifier.Classify("Dragon", "dragon Dragon DRAGON");

        result.Should().ContainSingle().Which.Should().Be("Fantasy");
    }
}
