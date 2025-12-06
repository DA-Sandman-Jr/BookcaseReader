using BookshelfReader.Core.Options;
using FluentAssertions;
using Xunit;

namespace BookshelfReader.Tests.Options;

public class ParsingOptionsTests
{
    [Fact]
    public void SectionName_MatchesExpectedValue()
    {
        ParsingOptions.SectionName.Should().Be("Parsing");
    }

    [Fact]
    public void Defaults_AreInitialized()
    {
        var options = new ParsingOptions();

        options.BaseConfidence.Should().Be(0.35);
        options.CommonAuthorTokens.Should().BeEquivalentTo("by", "author", "edited by");
    }
}
