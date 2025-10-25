using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BookshelfReader.Core.Options;
using BookshelfReader.Infrastructure.Segmentation;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenCvSharp;
using Xunit;

namespace BookshelfReader.Tests.Segmentation;

public sealed class OpenCvBookSegmentationServiceTests
{
    [Fact]
    public async Task SegmentAsync_StopsProcessingWhenMaxSegmentsReached()
    {
        var options = Options.Create(new SegmentationOptions
        {
            MaxSegments = 3,
            MinAreaFraction = 0.001,
            MaxAreaFraction = 0.5,
            MinAspectRatio = 0.2,
            MaxAspectRatio = 5.0
        });

        var logger = new TestLogger<OpenCvBookSegmentationService>();
        var service = new OpenCvBookSegmentationService(options, logger);

        using var image = new Mat(new Size(400, 400), MatType.CV_8UC3, Scalar.White);
        for (var i = 0; i < 6; i++)
        {
            var x = 20 + (i * 60);
            var topLeft = new Point(x, 40);
            var bottomRight = new Point(x + 40, 360);
            Cv2.Rectangle(image, topLeft, bottomRight, Scalar.Black, -1);
        }

        Cv2.ImEncode(".png", image, out var buffer).Should().BeTrue();
        await using var stream = new MemoryStream(buffer);

        var result = await service.SegmentAsync(stream, CancellationToken.None);

        result.Should().HaveCount(options.Value.MaxSegments);
        logger.Entries.Should().Contain(entry =>
            entry.LogLevel == LogLevel.Information &&
            entry.Message.Contains("Max segment limit of", StringComparison.Ordinal));
        result.Select(segment => segment.BoundingBox.X)
            .Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task SegmentAsync_ThrowsWhenImageExceedsPixelLimit()
    {
        var options = Options.Create(new SegmentationOptions
        {
            MaxImagePixels = 10_000
        });

        var logger = new TestLogger<OpenCvBookSegmentationService>();
        var service = new OpenCvBookSegmentationService(options, logger);

        using var image = new Mat(new Size(400, 400), MatType.CV_8UC3, Scalar.White);
        Cv2.ImEncode(".png", image, out var buffer).Should().BeTrue();
        await using var stream = new MemoryStream(buffer);

        var action = async () => await service.SegmentAsync(stream, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*pixels*");
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }

        public readonly record struct LogEntry(LogLevel LogLevel, string Message);

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
