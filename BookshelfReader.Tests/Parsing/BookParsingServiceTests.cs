using BookshelfReader.Core.Models;
using BookshelfReader.Core.Options;
using BookshelfReader.Infrastructure.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BookshelfReader.Tests.Parsing;

public class BookParsingServiceTests
{
    [Fact]
    public void Parse_PreservesAccentedCharacters()
    {
        var service = CreateService();
        var segment = new BookSegment { BoundingBox = new Rect(0, 0, 10, 10) };
        var ocr = new OcrResult
        {
            Text = "CIEN AÑOS DE SOLEDAD\nby GABRIEL GARCÍA MÁRQUEZ",
            Confidence = 0.8
        };

        var result = service.Parse(segment, ocr);

        result.Title.Should().Be("Cien Años De Soledad");
        result.Author.Should().Be("Gabriel García Márquez");
    }

    [Fact]
    public void Parse_PreservesNonLatinLetters()
    {
        var service = CreateService();
        var segment = new BookSegment { BoundingBox = new Rect(0, 0, 10, 10) };
        var ocr = new OcrResult
        {
            Text = "百年孤独",
            Confidence = 0.5
        };

        var result = service.Parse(segment, ocr);

        result.Title.Should().Be("百年孤独");
        result.Author.Should().BeEmpty();
    }

    [Fact]
    public void Parse_TrimsPunctuationWhileKeepingLetters()
    {
        var service = CreateService();
        var segment = new BookSegment { BoundingBox = new Rect(0, 0, 10, 10) };
        var ocr = new OcrResult
        {
            Text = "LES MISÉRABLES!!!\nBY VICTOR HUGO",
            Confidence = 0.75
        };

        var result = service.Parse(segment, ocr);

        result.Title.Should().Be("Les Misérables");
        result.Author.Should().Be("Victor Hugo");
    }

    private static BookParsingService CreateService(ParsingOptions? options = null)
    {
        return new BookParsingService(
            Options.Create(options ?? new ParsingOptions()),
            NullLogger<BookParsingService>.Instance);
    }
}
