using System.Diagnostics;
using BookshelfReader.Core.Abstractions;
using BookshelfReader.Core.Models;
using Microsoft.Extensions.Logging;

namespace BookshelfReader.Infrastructure.Processing;

public sealed class BookshelfProcessingService : IBookshelfProcessingService
{
    private readonly IBookSegmentationService _segmentationService;
    private readonly IBookSegmentProcessor _segmentProcessor;
    private readonly IBookEnrichmentService _enrichmentService;
    private readonly ILogger<BookshelfProcessingService> _logger;

    public BookshelfProcessingService(
        IBookSegmentationService segmentationService,
        IBookSegmentProcessor segmentProcessor,
        IBookEnrichmentService enrichmentService,
        ILogger<BookshelfProcessingService> logger)
    {
        _segmentationService = segmentationService;
        _segmentProcessor = segmentProcessor;
        _enrichmentService = enrichmentService;
        _logger = logger;
    }

    public async Task<ParseResult> ProcessAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var imageId = Guid.NewGuid();

        IReadOnlyList<BookSegment> segments = await _segmentationService.SegmentAsync(imageStream, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Segmentation produced {Count} segments for image {ImageId}", segments.Count, imageId);
        var books = new List<BookCandidate>();
        var diagnostics = new DiagnosticsBuilder(segments.Count);

        for (int index = 0; index < segments.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SegmentProcessingResult segmentResult = await _segmentProcessor.ProcessAsync(
                segments[index],
                index,
                imageId,
                cancellationToken).ConfigureAwait(false);

            if (segmentResult.Candidate is not null)
            {
                books.Add(segmentResult.Candidate);
            }

            diagnostics.AddNotes(AddSegmentContext(index, segmentResult.Notes));
        }

        if (books.Count > 0)
        {
            await _enrichmentService.EnrichAsync(books, cancellationToken).ConfigureAwait(false);
            int enriched = books.Count(b => b.Metadata is not null);
            _logger.LogInformation("Enriched {Enriched}/{Total} candidates for image {ImageId}", enriched, books.Count, imageId);
        }

        stopwatch.Stop();

        diagnostics.SetElapsed(stopwatch.ElapsedMilliseconds);

        return new ParseResult
        {
            ImageId = imageId,
            Books = books,
            Diagnostics = diagnostics.Build()
        };
    }

    private static IEnumerable<string> AddSegmentContext(int index, IEnumerable<string> notes)
    {
        string prefix = $"Segment {index}";

        return notes.Select(note =>
            note.StartsWith(prefix, StringComparison.Ordinal)
                ? note
                : $"{prefix}: {note}");
    }
}
