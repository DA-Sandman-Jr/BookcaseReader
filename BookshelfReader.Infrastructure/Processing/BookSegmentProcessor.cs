using System.Collections.Generic;
using System.Linq;
using BookshelfReader.Core.Abstractions;
using BookshelfReader.Core.Models;
using Microsoft.Extensions.Logging;

namespace BookshelfReader.Infrastructure.Processing;

public interface IBookSegmentProcessor
{
    Task<SegmentProcessingResult> ProcessAsync(BookSegment segment, int index, Guid imageId, CancellationToken cancellationToken);
}

public sealed record SegmentProcessingResult(BookCandidate? Candidate, IReadOnlyList<string> Notes)
{
    public static SegmentProcessingResult Failure(string note) => new(null, new[] { note });
}

public sealed class BookSegmentProcessor : IBookSegmentProcessor
{
    private readonly IOcrService _ocrService;
    private readonly IBookParsingService _parsingService;
    private readonly IGenreClassifier _genreClassifier;
    private readonly ILogger<BookSegmentProcessor> _logger;

    public BookSegmentProcessor(
        IOcrService ocrService,
        IBookParsingService parsingService,
        IGenreClassifier genreClassifier,
        ILogger<BookSegmentProcessor> logger)
    {
        _ocrService = ocrService;
        _parsingService = parsingService;
        _genreClassifier = genreClassifier;
        _logger = logger;
    }

    public async Task<SegmentProcessingResult> ProcessAsync(
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

            return new SegmentProcessingResult(candidate, Array.Empty<string>());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process segment {SegmentIndex} for image {ImageId}", index, imageId);
            return SegmentProcessingResult.Failure($"Segment {index}: {ex.Message}");
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
