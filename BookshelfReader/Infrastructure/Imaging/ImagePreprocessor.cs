using OpenCvSharp;

namespace BookshelfReader.Infrastructure.Imaging;

/// <summary>
/// Normalizes an uploaded image before it is sent to the vision model:
/// applies the EXIF orientation so the image is upright, downscales it so its
/// longest edge does not exceed a configured size, and re-encodes it as JPEG.
/// Re-encoding via OpenCV produces a fresh JPEG with no metadata segments, so
/// this also strips all EXIF data - including phone GPS coordinates - before
/// the image leaves the server.
/// </summary>
public static class ImagePreprocessor
{
    private const int JpegQuality = 85;

    public static byte[] Preprocess(byte[] imageData, int maxImageDimension)
    {
        ArgumentNullException.ThrowIfNull(imageData);

        int orientation = ExifOrientationReader.ReadOrientation(imageData);

        // OpenCV applies EXIF orientation automatically by default, which would
        // double-apply the correction performed below via ApplyOrientation.
        using Mat source = Cv2.ImDecode(imageData, ImreadModes.Color | ImreadModes.IgnoreOrientation);
        if (source.Empty())
        {
            throw new InvalidOperationException("Unable to decode the uploaded image.");
        }

        using Mat oriented = ApplyOrientation(source, orientation);
        using Mat resized = Downscale(oriented, maxImageDimension);

        Cv2.ImEncode(".jpg", resized, out byte[] encoded, new ImageEncodingParam(ImwriteFlags.JpegQuality, JpegQuality));
        return encoded;
    }

    private static Mat ApplyOrientation(Mat source, int orientation)
    {
        var result = new Mat();
        switch (orientation)
        {
            case 2:
                Cv2.Flip(source, result, FlipMode.Y);
                return result;
            case 3:
                Cv2.Rotate(source, result, RotateFlags.Rotate180);
                return result;
            case 4:
                Cv2.Flip(source, result, FlipMode.X);
                return result;
            case 5:
                Cv2.Rotate(source, result, RotateFlags.Rotate90Clockwise);
                Cv2.Flip(result, result, FlipMode.Y);
                return result;
            case 6:
                Cv2.Rotate(source, result, RotateFlags.Rotate90Clockwise);
                return result;
            case 7:
                Cv2.Rotate(source, result, RotateFlags.Rotate90Counterclockwise);
                Cv2.Flip(result, result, FlipMode.Y);
                return result;
            case 8:
                Cv2.Rotate(source, result, RotateFlags.Rotate90Counterclockwise);
                return result;
            default:
                result.Dispose();
                return source.Clone();
        }
    }

    private static Mat Downscale(Mat source, int maxImageDimension)
    {
        int longestEdge = Math.Max(source.Width, source.Height);
        if (maxImageDimension <= 0 || longestEdge <= maxImageDimension)
        {
            return source.Clone();
        }

        double scale = (double)maxImageDimension / longestEdge;
        var size = new Size(
            Math.Max(1, (int)Math.Round(source.Width * scale)),
            Math.Max(1, (int)Math.Round(source.Height * scale)));

        var resized = new Mat();
        Cv2.Resize(source, resized, size, 0, 0, InterpolationFlags.Area);
        return resized;
    }
}
