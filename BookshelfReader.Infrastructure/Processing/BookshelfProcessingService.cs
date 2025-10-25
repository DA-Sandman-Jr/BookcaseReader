using System.Collections.Generic;
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
            cancellationToken.ThrowIfCancellationRequested();

            var (candidate, errorNote) = await ProcessSegmentAsync(
                    segments[index],
                    index,
                    imageId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (candidate is not null)
            {
                books.Add(candidate);
            }

            if (!string.IsNullOrEmpty(errorNote))
            {
                diagnosticNotes.Add(errorNote);
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

    private async Task<(BookCandidate? Candidate, string? ErrorNote)> ProcessSegmentAsync(
        BookSegment segment,
        int index,
        Guid imageId,
        CancellationToken cancellationToken)
    {
        try
        {
            var ocrResult = await _ocrService.RecognizeAsync(segment.ImageData, cancellationToken).ConfigureAwait(false);
            var candidate = _parsingService.Parse(segment, ocrResult);
            candidate.Genres = _genreClassifier.Classify(candidate.Title, candidate.RawText).ToList();
            AddRotationNotes(candidate, ocrResult.Attempts);

            return (candidate, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process segment {SegmentIndex} for image {ImageId}", index, imageId);
            return (null, $"Segment {index}: {ex.Message}");
        }
    }

    private static void AddRotationNotes(BookCandidate candidate, IReadOnlyList<string> attempts)
    {
        if (attempts is null || attempts.Count <= 1)
        {
            return;
        }

        var labels = attempts
            .Select((_, attemptIndex) => attemptIndex switch
            {
                0 => "0°",
                1 => "90°",
                2 => "270°",
                _ => "other"
            })
            .ToArray();

        candidate.Notes.Add("OCR tried rotations: " + string.Join(", ", labels));
    }
}
