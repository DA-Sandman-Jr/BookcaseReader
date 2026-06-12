using BookshelfReader.Infrastructure.Imaging;
using FluentAssertions;
using Xunit;

namespace BookshelfReader.Tests.Imaging;

public class ExifOrientationReaderTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void ReadOrientation_ReadsOrientationTagFromApp1Segment(int orientation)
    {
        byte[] jpegBytes = JpegTestImages.WithExifOrientation(JpegTestImages.CreatePlainJpeg(8, 8), orientation);

        ExifOrientationReader.ReadOrientation(jpegBytes).Should().Be(orientation);
    }

    [Fact]
    public void ReadOrientation_WithoutApp1Segment_ReturnsDefaultOrientation()
    {
        byte[] jpegBytes = JpegTestImages.CreatePlainJpeg(8, 8);

        ExifOrientationReader.ReadOrientation(jpegBytes).Should().Be(1);
    }

    [Fact]
    public void ReadOrientation_NonJpegData_ReturnsDefaultOrientation()
    {
        byte[] data = { 0x00, 0x01, 0x02, 0x03, 0x04 };

        ExifOrientationReader.ReadOrientation(data).Should().Be(1);
    }

    [Fact]
    public void ReadOrientation_TooShortToContainAMarker_ReturnsDefaultOrientation()
    {
        byte[] data = { 0xFF, 0xD8 };

        ExifOrientationReader.ReadOrientation(data).Should().Be(1);
    }

    [Fact]
    public void ReadOrientation_OutOfRangeOrientationValue_ReturnsDefaultOrientation()
    {
        byte[] jpegBytes = JpegTestImages.WithExifOrientation(JpegTestImages.CreatePlainJpeg(8, 8), orientation: 9);

        ExifOrientationReader.ReadOrientation(jpegBytes).Should().Be(1);
    }
}
