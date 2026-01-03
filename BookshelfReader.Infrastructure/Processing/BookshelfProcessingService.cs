using System.Collections.Generic;
using System.Diagnostics;
using BookshelfReader.Core.Abstractions;
using BookshelfReader.Core.Models;
using Microsoft.Extensions.Logging;

namespace BookshelfReader.Infrastructure.Processing;

public sealed class BookshelfProcessingService : IBookshelfProcessingService
{
    private readonly IBookSegmentationService _segmentationService;
    private readonly IBookSegmentProcessor _segmentProcessor;
    private readonly ILogger<BookshelfProcessingService> _logger;

    public BookshelfProcessingService(
        IBookSegmentationService segmentationService,
        IBookSegmentProcessor segmentProcessor,
        ILogger<BookshelfProcessingService> logger)
    {
        _segmentationService = segmentationService;
        _segmentProcessor = segmentProcessor;
        _logger = logger;
    }

    public async Task<ParseResult> ProcessAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var imageId = Guid.NewGuid();

        var segments = await _segmentationService.SegmentAsync(imageStream, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Segmentation produced {Count} segments for image {ImageId}", segments.Count, imageId);
        var books = new List<BookCandidate>();
        var diagnostics = new DiagnosticsBuilder(segments.Count);

        for (var index = 0; index < segments.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var segmentResult = await _segmentProcessor.ProcessAsync(
                segments[index],
                index,
                imageId,
                cancellationToken).ConfigureAwait(false);

            if (segmentResult.Candidate is not null)
            {
                books.Add(segmentResult.Candidate);
            }

            diagnostics.AddNotes(segmentResult.Notes);
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
}
