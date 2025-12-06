using System;
using BookshelfReader.Core.Options;
using FluentAssertions;
using Xunit;

namespace BookshelfReader.Tests.Options;

public class TesseractOcrOptionsTests
{
    [Fact]
    public void SectionName_MatchesExpectedValue()
    {
        TesseractOcrOptions.SectionName.Should().Be("Ocr:Tesseract");
    }

    [Fact]
    public void Defaults_AreInitialized()
    {
        var options = new TesseractOcrOptions();

        options.DataPath.Should().Be(string.Empty);
        options.Language.Should().Be("eng");
        options.MaxDegreeOfParallelism.Should().Be(Environment.ProcessorCount);
    }
}
