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
        var segmentProcessor = new SequenceSegmentProcessor(new[]
        {
            new SegmentProcessingResult(new BookCandidate { Title = "First" }, Array.Empty<string>()),
            SegmentProcessingResult.Failure("boom")
        });

        var service = new BookshelfProcessingService(
            segmentationService,
            segmentProcessor,
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

    private sealed class SequenceSegmentProcessor : IBookSegmentProcessor
    {
        private readonly Queue<SegmentProcessingResult> _results;

        public SequenceSegmentProcessor(IEnumerable<SegmentProcessingResult> results)
        {
            _results = new Queue<SegmentProcessingResult>(results);
        }

        public Task<SegmentProcessingResult> ProcessAsync(BookSegment segment, int index, Guid imageId, CancellationToken cancellationToken)
        {
            if (_results.Count == 0)
            {
                throw new InvalidOperationException("No segment results configured.");
            }

            return Task.FromResult(_results.Dequeue());
        }
    }
}
