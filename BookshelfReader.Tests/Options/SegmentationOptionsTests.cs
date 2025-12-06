using BookshelfReader.Core.Options;
using FluentAssertions;
using Xunit;

namespace BookshelfReader.Tests.Options;

public class SegmentationOptionsTests
{
    [Fact]
    public void SectionName_MatchesExpectedValue()
    {
        SegmentationOptions.SectionName.Should().Be("Segmentation");
    }

    [Fact]
    public void Defaults_AreInitialized()
    {
        var options = new SegmentationOptions();

        options.MinAspectRatio.Should().Be(0.1);
        options.MaxAspectRatio.Should().Be(10.0);
        options.MinAreaFraction.Should().Be(0.0025);
        options.MaxAreaFraction.Should().Be(0.9);
        options.MaxSegments.Should().Be(64);
        options.MaxImagePixels.Should().Be(25_000_000);
    }
}
