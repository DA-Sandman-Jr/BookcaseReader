using System;
using System.Collections.Generic;
using BookshelfReader.Core.Options;
using FluentAssertions;
using Xunit;

namespace BookshelfReader.Tests.Options;

public class UploadsOptionsTests
{
    [Fact]
    public void IsSupportedContentType_ReturnsFalse_ForUnsupportedImage()
    {
        UploadsOptions.IsSupportedContentType("image/gif").Should().BeFalse();
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/jpg")]
    [InlineData("image/pjpeg")]
    public void IsSupportedContentType_AcceptsCommonJpegAliases(string alias)
    {
        UploadsOptions.IsSupportedContentType(alias).Should().BeTrue();
    }

    [Fact]
    public void AllowedContentTypes_FiltersUnsupportedEntries()
    {
        var options = new UploadsOptions
        {
            AllowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "image/gif",
                "image/jpeg"
            }
        };

        options.AllowedContentTypes.Should().BeEquivalentTo(new[] { "image/jpeg" });
    }

    [Fact]
    public void AllowedContentTypes_NormalizesAliases()
    {
        var options = new UploadsOptions
        {
            AllowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "image/jpg",
                "image/pjpeg"
            }
        };

        options.AllowedContentTypes.Should().BeEquivalentTo(new[] { "image/jpeg" });
    }

    [Fact]
    public void TryGetImageSignature_ReturnsSameSignature_ForAliases()
    {
        UploadsOptions.TryGetImageSignature("image/jpg", out var aliasSignature).Should().BeTrue();
        UploadsOptions.TryGetImageSignature("image/jpeg", out var canonicalSignature).Should().BeTrue();

        aliasSignature.ToArray().Should().Equal(canonicalSignature.ToArray());
    }
}
