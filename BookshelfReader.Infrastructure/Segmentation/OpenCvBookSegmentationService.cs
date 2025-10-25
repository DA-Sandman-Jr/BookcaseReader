using System;
using System.Linq;
using BookshelfReader.Core.Abstractions;
using BookshelfReader.Core.Models;
using BookshelfReader.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace BookshelfReader.Infrastructure.Segmentation;

public sealed class OpenCvBookSegmentationService : IBookSegmentationService
{
    private readonly SegmentationOptions _options;
    private readonly ILogger<OpenCvBookSegmentationService> _logger;

    public OpenCvBookSegmentationService(IOptions<SegmentationOptions> options, ILogger<OpenCvBookSegmentationService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BookSegment>> SegmentAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        using var ms = new MemoryStream();
        await imageStream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        var data = ms.ToArray();

        if (data.Length == 0)
        {
            return Array.Empty<BookSegment>();
        }

        using var sourceMat = Cv2.ImDecode(data, ImreadModes.Color);
        cancellationToken.ThrowIfCancellationRequested();
        if (sourceMat.Empty())
        {
            _logger.LogWarning("Unable to decode uploaded image for segmentation");
            return Array.Empty<BookSegment>();
        }

        var pixelCount = (long)sourceMat.Width * sourceMat.Height;
        if (pixelCount > _options.MaxImagePixels)
        {
            throw new InvalidOperationException(
                $"Uploaded image has {pixelCount:N0} pixels which exceeds the configured limit of {_options.MaxImagePixels:N0}.");
        }

        using var gray = new Mat();
        Cv2.CvtColor(sourceMat, gray, ColorConversionCodes.BGR2GRAY);
        cancellationToken.ThrowIfCancellationRequested();

        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);
        cancellationToken.ThrowIfCancellationRequested();

        using var edges = new Mat();
        Cv2.Canny(blurred, edges, 30, 120);
        cancellationToken.ThrowIfCancellationRequested();

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
        using var closed = new Mat();
        Cv2.MorphologyEx(edges, closed, MorphTypes.Close, kernel, iterations: 2);
        cancellationToken.ThrowIfCancellationRequested();

        Cv2.FindContours(closed, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        cancellationToken.ThrowIfCancellationRequested();

        if (contours.Length == 0)
        {
            return Array.Empty<BookSegment>();
        }

        var orderedContours = contours
            .Select(contour => new { Contour = contour, Rect = Cv2.BoundingRect(contour) })
            .OrderBy(item => item.Rect.X)
            .ToArray();
        cancellationToken.ThrowIfCancellationRequested();

        var segments = new List<BookSegment>();
        var imageArea = sourceMat.Width * sourceMat.Height;

        foreach (var item in orderedContours)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var contour = item.Contour;
            var rect = item.Rect;

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                continue;
            }

            var area = rect.Width * rect.Height;
            var areaFraction = area / (double)imageArea;
            if (areaFraction < _options.MinAreaFraction || areaFraction > _options.MaxAreaFraction)
            {
                continue;
            }

            var aspectRatio = rect.Width / (double)rect.Height;
            if (aspectRatio < _options.MinAspectRatio || aspectRatio > _options.MaxAspectRatio)
            {
                continue;
            }

            var rotatedRect = Cv2.MinAreaRect(contour);
            var angle = rotatedRect.Angle;
            var size = rotatedRect.Size;

            if (size.Width < size.Height)
            {
                angle += 90;
                size = new Size2f(size.Height, size.Width);
            }

            var rotationMatrix = Cv2.GetRotationMatrix2D(rotatedRect.Center, angle, 1.0);
            using var rotatedImage = new Mat();
            Cv2.WarpAffine(sourceMat, rotatedImage, rotationMatrix, sourceMat.Size(), InterpolationFlags.Linear, BorderTypes.Replicate);
            cancellationToken.ThrowIfCancellationRequested();

            var cropRect = new OpenCvSharp.Rect(
                (int)Math.Round(rotatedRect.Center.X - size.Width / 2),
                (int)Math.Round(rotatedRect.Center.Y - size.Height / 2),
                (int)Math.Round(size.Width),
                (int)Math.Round(size.Height));

            cropRect = cropRect & new OpenCvSharp.Rect(0, 0, rotatedImage.Width, rotatedImage.Height);
            if (cropRect.Width <= 0 || cropRect.Height <= 0)
            {
                continue;
            }

            using var cropped = new Mat(rotatedImage, cropRect);
            if (cropped.Empty())
            {
                continue;
            }

            if (!Cv2.ImEncode(".png", cropped, out var buffer))
            {
                continue;
            }
            cancellationToken.ThrowIfCancellationRequested();

            var boundingBox = new Rect(rect.X, rect.Y, rect.Width, rect.Height);
            segments.Add(new BookSegment
            {
                BoundingBox = boundingBox,
                ImageData = buffer
            });

            if (_options.MaxSegments > 0 && segments.Count >= _options.MaxSegments)
            {
                _logger.LogInformation("Max segment limit of {MaxSegments} reached; stopping contour processing.", _options.MaxSegments);
                break;
            }
        }

        return segments
            .OrderBy(s => s.BoundingBox.X)
            .Take(_options.MaxSegments)
            .ToArray();
    }
}
