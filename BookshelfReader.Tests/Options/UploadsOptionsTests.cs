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
}
