using System.Diagnostics;
using System.Linq;
using BookshelfReader.Core.Abstractions;
using BookshelfReader.Core.Models;
using Microsoft.Extensions.Logging;

namespace BookshelfReader.Infrastructure.Processing;

public sealed class BookshelfProcessingService : IBookshelfProcessingService
{
    private readonly IBookSegmentationService _segmentationService;
    private readonly IOcrService _ocrService;
    private readonly IBookParsingService _parsingService;
    private readonly IGenreClassifier _genreClassifier;
    private readonly ILogger<BookshelfProcessingService> _logger;

    public BookshelfProcessingService(
        IBookSegmentationService segmentationService,
        IOcrService ocrService,
        IBookParsingService parsingService,
        IGenreClassifier genreClassifier,
        ILogger<BookshelfProcessingService> logger)
    {
        _segmentationService = segmentationService;
        _ocrService = ocrService;
        _parsingService = parsingService;
        _genreClassifier = genreClassifier;
        _logger = logger;
    }

    public async Task<ParseResult> ProcessAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var imageId = Guid.NewGuid();

        var segments = await _segmentationService.SegmentAsync(imageStream, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Segmentation produced {Count} segments for image {ImageId}", segments.Count, imageId);
        var books = new List<BookCandidate>();
        var diagnosticNotes = new List<string>();

        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var ocrResult = await _ocrService.RecognizeAsync(segment.ImageData, cancellationToken).ConfigureAwait(false);
                var parsed = _parsingService.Parse(segment, ocrResult);
                parsed.Genres = _genreClassifier.Classify(parsed.Title, parsed.RawText).ToList();

                if (ocrResult.Attempts.Count > 1)
                {
                    parsed.Notes.Add("OCR tried rotations: " + string.Join(", ", ocrResult.Attempts.Select((_, attemptIndex) => attemptIndex switch
                    {
                        0 => "0°",
                        1 => "90°",
                        2 => "270°",
                        _ => "other"
                    })));
                }

                books.Add(parsed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process segment {SegmentIndex} for image {ImageId}", index, imageId);
                diagnosticNotes.Add($"Segment {index}: {ex.Message}");
            }
        }

        stopwatch.Stop();

        return new ParseResult
        {
            ImageId = imageId,
            Books = books,
            Diagnostics = new Diagnostics
            {
                SegmentCount = segments.Count,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Notes = diagnosticNotes
            }
        };
    }
}
