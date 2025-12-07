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

    [Fact]
    public void Parse_ReturnsZeroConfidenceAndNote_WhenTextIsEmpty()
    {
        var service = CreateService();
        var segment = new BookSegment { BoundingBox = new Rect(0, 0, 10, 10) };
        var ocr = new OcrResult { Text = string.Empty, Confidence = 0.9 };

        var result = service.Parse(segment, ocr);

        result.Confidence.Should().Be(0);
        result.Notes.Should().ContainSingle().Which.Should().Be("OCR returned no text");
        result.Title.Should().BeEmpty();
        result.Author.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ReturnsZeroConfidence_WhenLinesCannotBeParsed()
    {
        var service = CreateService();
        var segment = new BookSegment { BoundingBox = new Rect(0, 0, 10, 10) };
        var ocr = new OcrResult { Text = "!!! @@@ ###", Confidence = 0.6 };

        var result = service.Parse(segment, ocr);

        result.Confidence.Should().Be(0);
        result.Notes.Should().ContainSingle().Which.Should().Be("Unable to parse OCR lines");
        result.Title.Should().BeEmpty();
        result.Author.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ExtractsAuthorUsingCommonToken()
    {
        var service = CreateService();
        var segment = new BookSegment { BoundingBox = new Rect(0, 0, 10, 10) };
        var ocr = new OcrResult
        {
            Text = "THE GREAT GATSBY\nby F. Scott Fitzgerald",
            Confidence = 0.85
        };

        var result = service.Parse(segment, ocr);

        result.Title.Should().Be("The Great Gatsby");
        result.Author.Should().Be("F. Scott Fitzgerald");
    }

    [Fact]
    public void Parse_AddsAlternativeTitleNote_WhenCloseMatchExists()
    {
        var service = CreateService();
        var segment = new BookSegment { BoundingBox = new Rect(0, 0, 10, 10) };
        var ocr = new OcrResult
        {
            Text = "Harry Potter and the Philosopher's Stone\nHarry Potter & the Philosopher's Stone\nby J.K. Rowling",
            Confidence = 0.7
        };

        var result = service.Parse(segment, ocr);

        result.Notes.Should().Contain(n => n.StartsWith("Alternative title candidate:"));
    }

    [Fact]
    public void Parse_CalculatesConfidenceFromTextLengthAuthorAndOcrScore()
    {
        var options = new ParsingOptions { BaseConfidence = 0.3 };
        var service = CreateService(options);
        var segment = new BookSegment { BoundingBox = new Rect(0, 0, 10, 10) };
        var ocr = new OcrResult
        {
            Text = "A VERY LONG AND DESCRIPTIVE TITLE FOR TESTING\nBY FRANK HERBERT",
            Confidence = 0.8
        };

        var result = service.Parse(segment, ocr);

        result.Confidence.Should().BeApproximately(0.85, 0.0001);
    }

    private static BookParsingService CreateService(ParsingOptions? options = null)
    {
        return new BookParsingService(
            Options.Create(options ?? new ParsingOptions()),
            NullLogger<BookParsingService>.Instance);
    }
}
