using OpenCvSharp;

namespace BookshelfReader.Tests.Imaging;

/// <summary>
/// Builds small in-memory JPEGs for imaging tests, optionally with a synthetic
/// APP1/Exif segment carrying just the orientation tag (0x0112).
/// </summary>
internal static class JpegTestImages
{
    public static byte[] CreatePlainJpeg(int width, int height)
    {
        using var mat = new Mat(height, width, MatType.CV_8UC3, new Scalar(255, 255, 255));
        Cv2.ImEncode(".jpg", mat, out byte[] bytes);
        return bytes;
    }

    /// <summary>
    /// Inserts a minimal APP1/Exif segment with the given orientation (1-8)
    /// immediately after the SOI marker.
    /// </summary>
    public static byte[] WithExifOrientation(byte[] jpegBytes, int orientation)
    {
        byte[] app1 = BuildApp1ExifOrientationSegment(orientation);

        byte[] result = new byte[jpegBytes.Length + app1.Length];
        Buffer.BlockCopy(jpegBytes, 0, result, 0, 2);
        Buffer.BlockCopy(app1, 0, result, 2, app1.Length);
        Buffer.BlockCopy(jpegBytes, 2, result, 2 + app1.Length, jpegBytes.Length - 2);
        return result;
    }

    /// <summary>
    /// Builds a standalone APP1/Exif segment (marker + length + payload) for
    /// tests that exercise <c>ExifOrientationReader</c> directly.
    /// </summary>
    public static byte[] BuildApp1ExifOrientationSegment(int orientation)
    {
        byte[] exifHeader = { (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0x00, 0x00 };

        // TIFF header: "II" (little-endian), magic 0x002A, IFD0 offset = 8.
        byte[] tiffHeader = { 0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00 };

        // IFD0: entry count (1) + one 12-byte entry (tag 0x0112, type SHORT, count 1, value) + next-IFD offset (0).
        byte[] ifd0 = new byte[2 + 12 + 4];
        ifd0[0] = 0x01;
        ifd0[1] = 0x00;
        ifd0[2] = 0x12; // tag low byte (0x0112)
        ifd0[3] = 0x01; // tag high byte
        ifd0[4] = 0x03; // type = SHORT
        ifd0[5] = 0x00;
        ifd0[6] = 0x01; // count = 1
        ifd0[7] = 0x00;
        ifd0[8] = 0x00;
        ifd0[9] = 0x00;
        ifd0[10] = (byte)orientation;
        ifd0[11] = 0x00;
        // ifd0[12..13] padding, ifd0[14..17] next-IFD offset = 0, both already zero.

        byte[] payload = exifHeader.Concat(tiffHeader).Concat(ifd0).ToArray();

        int segmentLength = payload.Length + 2;
        byte[] segment = new byte[4 + payload.Length];
        segment[0] = 0xFF;
        segment[1] = 0xE1;
        segment[2] = (byte)(segmentLength >> 8);
        segment[3] = (byte)(segmentLength & 0xFF);
        Buffer.BlockCopy(payload, 0, segment, 4, payload.Length);
        return segment;
    }
}
