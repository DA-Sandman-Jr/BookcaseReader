using BookshelfReader.Core.Models;
using BookshelfReader.Core.Options;
using BookshelfReader.Infrastructure.Segmentation;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenCvSharp;
using Xunit;

using LoggingLogLevel = Microsoft.Extensions.Logging.LogLevel;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace BookshelfReader.Tests.Segmentation;

public sealed class OpenCvBookSegmentationServiceTests
{
    [Fact]
    public async Task SegmentAsync_StopsProcessingWhenMaxSegmentsReached()
    {
        IOptions<SegmentationOptions> options = OptionsFactory.Create(new SegmentationOptions
        {
            MaxSegments = 3,
            MinAreaFraction = 0.001,
            MaxAreaFraction = 0.5,
            MinAspectRatio = 0.1,
            MaxAspectRatio = 5.0
        });

        var logger = new TestLogger<OpenCvBookSegmentationService>();
        var service = new OpenCvBookSegmentationService(options, logger);

        using var image = new Mat(new Size(400, 400), MatType.CV_8UC3, Scalar.White);
        for (int i = 0; i < 6; i++)
        {
            int x = 20 + (i * 60);
            var topLeft = new Point(x, 40);
            var bottomRight = new Point(x + 40, 360);
            Cv2.Rectangle(image, topLeft, bottomRight, Scalar.Black, -1);
        }

        Cv2.ImEncode(".png", image, out byte[]? buffer).Should().BeTrue();
        await using var stream = new MemoryStream(buffer);

        IReadOnlyList<BookSegment> result = await service.SegmentAsync(stream, CancellationToken.None);

        result.Should().HaveCount(options.Value.MaxSegments);
        logger.Entries.Should().Contain(entry =>
            entry.LogLevel == LoggingLogLevel.Information &&
            entry.Message.Contains("Max segment limit of", StringComparison.Ordinal));
        result.Select(segment => segment.BoundingBox.X)
            .Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task SegmentAsync_ThrowsWhenImageExceedsPixelLimit()
    {
        IOptions<SegmentationOptions> options = OptionsFactory.Create(new SegmentationOptions
        {
            MaxImagePixels = 10_000
        });

        var logger = new TestLogger<OpenCvBookSegmentationService>();
        var service = new OpenCvBookSegmentationService(options, logger);

        using var image = new Mat(new Size(400, 400), MatType.CV_8UC3, Scalar.White);
        Cv2.ImEncode(".png", image, out byte[]? buffer).Should().BeTrue();
        await using var stream = new MemoryStream(buffer);

        Func<Task<IReadOnlyList<BookSegment>>> action = async () => await service.SegmentAsync(stream, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*pixels*");
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LoggingLogLevel logLevel) => true;

        public void Log<TState>(LoggingLogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }

        public readonly record struct LogEntry(LoggingLogLevel LogLevel, string Message);

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
