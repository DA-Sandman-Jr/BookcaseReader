using BookshelfReader.Core.Abstractions;
using BookshelfReader.Core.Models;
using BookshelfReader.Infrastructure.Processing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookshelfReader.Tests.Processing;

public class BookshelfProcessingServiceTests
{
    [Fact]
    public async Task ProcessAsync_WhenSegmentProcessingFails_ContinuesWithOtherSegments()
    {
        var segments = new List<BookSegment>
        {
            new() { BoundingBox = new Rect(0, 0, 10, 10), ImageData = new byte[] { 1 } },
            new() { BoundingBox = new Rect(10, 0, 10, 10), ImageData = new byte[] { 2 } }
        };

        var segmentationService = new StubSegmentationService(segments);
        var ocrService = new SequenceOcrService(new Func<OcrResult>[]
        {
            () => new OcrResult { Text = "First", Attempts = new[] { "0" } },
            () => throw new InvalidOperationException("boom")
        });
        var parsingService = new StubParsingService();
        var genreClassifier = new StubGenreClassifier();

        var service = new BookshelfProcessingService(
            segmentationService,
            ocrService,
            parsingService,
            genreClassifier,
            NullLogger<BookshelfProcessingService>.Instance);

        using var stream = new MemoryStream();
        var result = await service.ProcessAsync(stream);

        result.Books.Should().HaveCount(1);
        result.Books.Single().Title.Should().Be("First");
        result.Diagnostics.SegmentCount.Should().Be(2);
        result.Diagnostics.Notes.Should().ContainSingle(note => note.Contains("Segment 1") && note.Contains("boom"));
    }

    private sealed class StubSegmentationService : IBookSegmentationService
    {
        private readonly IReadOnlyList<BookSegment> _segments;

        public StubSegmentationService(IReadOnlyList<BookSegment> segments)
        {
            _segments = segments;
        }

        public Task<IReadOnlyList<BookSegment>> SegmentAsync(Stream imageStream, CancellationToken cancellationToken = default)
            => Task.FromResult(_segments);
    }

    private sealed class SequenceOcrService : IOcrService
    {
        private readonly Queue<Func<OcrResult>> _results;

        public SequenceOcrService(IEnumerable<Func<OcrResult>> results)
        {
            _results = new Queue<Func<OcrResult>>(results);
        }

        public Task<OcrResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken = default)
        {
            if (_results.Count == 0)
            {
                throw new InvalidOperationException("No OCR results configured.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var next = _results.Dequeue();
            return Task.FromResult(next());
        }
    }

    private sealed class StubParsingService : IBookParsingService
    {
        public BookCandidate Parse(BookSegment segment, OcrResult ocr)
            => new()
            {
                BoundingBox = segment.BoundingBox,
                Title = ocr.Text,
                RawText = ocr.Text
            };
    }

    private sealed class StubGenreClassifier : IGenreClassifier
    {
        public IReadOnlyList<string> Classify(string title, string rawText)
            => Array.Empty<string>();
    }
}
