using System.Globalization;
using System.Text.RegularExpressions;
using BookshelfReader.Core.Abstractions;
using BookshelfReader.Core.Models;
using BookshelfReader.Core.Options;
using FuzzySharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookshelfReader.Infrastructure.Parsing;

public sealed class BookParsingService : IBookParsingService
{
    private static readonly Regex CleanupRegex = new("[^A-Za-z0-9'&,:;\- ]", RegexOptions.Compiled);
    private readonly ParsingOptions _options;
    private readonly ILogger<BookParsingService> _logger;

    public BookParsingService(IOptions<ParsingOptions> options, ILogger<BookParsingService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public BookCandidate Parse(BookSegment segment, OcrResult ocr)
    {
        var candidate = new BookCandidate
        {
            BoundingBox = segment.BoundingBox,
            RawText = ocr.Text
        };

        if (string.IsNullOrWhiteSpace(ocr.Text))
        {
            candidate.Confidence = 0;
            candidate.Notes.Add("OCR returned no text");
            _logger.LogDebug("OCR returned empty text for bounding box {BoundingBox}", segment.BoundingBox);
            return candidate;
        }

        var lines = ocr.Text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => CleanupRegex.Replace(l, " ").Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct()
            .ToList();

        if (lines.Count == 0)
        {
            candidate.Confidence = 0;
            candidate.Notes.Add("Unable to parse OCR lines");
            _logger.LogDebug("OCR produced no parsable lines for bounding box {BoundingBox}", segment.BoundingBox);
            return candidate;
        }

        var probableTitle = lines.OrderByDescending(l => l.Length).First();
        candidate.Title = ToTitleCase(probableTitle);

        var authorLine = FindAuthorLine(lines);
        if (!string.IsNullOrEmpty(authorLine))
        {
            candidate.Author = ToTitleCase(authorLine);
        }

        var confidence = _options.BaseConfidence;
        if (!string.IsNullOrEmpty(candidate.Title))
        {
            confidence += Math.Min(0.4, candidate.Title.Length / 50d);
        }

        if (!string.IsNullOrEmpty(candidate.Author))
        {
            confidence += 0.2;
        }

        if (ocr.Confidence > 0)
        {
            confidence = (confidence + ocr.Confidence) / 2;
        }

        candidate.Confidence = Math.Clamp(confidence, 0, 0.99);

        if (lines.Count > 1)
        {
            var best = Process.ExtractTop(probableTitle, lines, limit: 3);
            if (best.Count > 1 && best[1].Score > 90 && best[1].Value != probableTitle)
            {
                candidate.Notes.Add($"Alternative title candidate: {best[1].Value}");
            }
        }

        return candidate;
    }

    private string FindAuthorLine(List<string> lines)
    {
        foreach (var line in lines)
        {
            if (_options.CommonAuthorTokens.Any(t => line.Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                var token = _options.CommonAuthorTokens
                    .First(t => line.Contains(t, StringComparison.OrdinalIgnoreCase));
                var author = line[(line.IndexOf(token, StringComparison.OrdinalIgnoreCase) + token.Length)..].Trim();
                if (!string.IsNullOrEmpty(author))
                {
                    return author;
                }
            }
        }

        var probableAuthor = lines
            .Where(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 4)
            .OrderBy(l => l.Length)
            .FirstOrDefault(l => l.Any(char.IsLetter) && l.Split(' ').All(w => w.Length <= 12));

        return probableAuthor ?? string.Empty;
    }

    private static string ToTitleCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
    }
}
