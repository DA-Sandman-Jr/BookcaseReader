using BookshelfReader.Core.Options;
using FluentAssertions;
using Xunit;

namespace BookshelfReader.Tests.Options;

public class UploadsOptionsTests
{
    private static readonly string[] JpegContentTypes = { "image/jpeg" };

    [Fact]
    public void IsSupportedContentType_ReturnsFalse_ForUnsupportedImage()
    {
        UploadsOptions.IsSupportedContentType("image/gif").Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetCanonicalContentType_ReturnsFalse_ForMissingValues(string? input)
    {
        UploadContentTypeHelper.TryGetCanonicalContentType(input, out string? canonical).Should().BeFalse();
        canonical.Should().BeEmpty();
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

        options.AllowedContentTypes.Should().BeEquivalentTo(JpegContentTypes);
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

        options.AllowedContentTypes.Should().BeEquivalentTo(JpegContentTypes);
    }

    [Fact]
    public void TryGetImageSignature_ReturnsSameSignature_ForAliases()
    {
        UploadContentTypeHelper.TryGetImageSignature("image/jpg", out ReadOnlyMemory<byte> aliasSignature).Should().BeTrue();
        UploadContentTypeHelper.TryGetImageSignature("image/jpeg", out ReadOnlyMemory<byte> canonicalSignature).Should().BeTrue();

        aliasSignature.ToArray().Should().Equal(canonicalSignature.ToArray());
    }
}
