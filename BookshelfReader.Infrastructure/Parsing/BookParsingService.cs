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
    private static readonly Regex CleanupRegex = new(
        @"[^\p{L}\p{M}\p{Nd}'&,:;\.\- ]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly char[] LineSeparators = { '\r', '\n' };
    private static readonly char[] EdgePunctuation = { '.', ',', ':', ';', '-' };

    private readonly ParsingOptions _options;
    private readonly ILogger<BookParsingService> _logger;

    public BookParsingService(IOptions<ParsingOptions> options, ILogger<BookParsingService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public BookCandidate Parse(BookSegment segment, OcrResult ocr)
    {
        ArgumentNullException.ThrowIfNull(segment);
        ArgumentNullException.ThrowIfNull(ocr);

        BookCandidate candidate = CreateCandidate(segment, ocr);
        if (string.IsNullOrWhiteSpace(candidate.RawText))
        {
            return HandleEmptyText(candidate, segment);
        }

        List<string> lines = NormalizeLines(candidate.RawText);
        if (lines.Count == 0)
        {
            return HandleUnparsableText(candidate, segment);
        }

        string probableTitle = DetermineTitle(lines);
        candidate.Title = ToTitleCase(probableTitle);
        candidate.Author = DetermineAuthor(lines, probableTitle);

        candidate.Confidence = CalculateConfidence(candidate.Title, candidate.Author, ocr.Confidence);
        AddAlternativeTitleNotes(candidate, probableTitle, lines);

        return candidate;
    }

    private static BookCandidate CreateCandidate(BookSegment segment, OcrResult ocr)
    {
        return new BookCandidate
        {
            BoundingBox = segment.BoundingBox,
            RawText = ocr.Text
        };
    }

    private BookCandidate HandleEmptyText(BookCandidate candidate, BookSegment segment)
    {
        candidate.Confidence = 0;
        candidate.Notes.Add("OCR returned no text");
        _logger.LogDebug("OCR returned empty text for bounding box {BoundingBox}", segment.BoundingBox);
        return candidate;
    }

    private BookCandidate HandleUnparsableText(BookCandidate candidate, BookSegment segment)
    {
        candidate.Confidence = 0;
        candidate.Notes.Add("Unable to parse OCR lines");
        _logger.LogDebug("OCR produced no parsable lines for bounding box {BoundingBox}", segment.BoundingBox);
        return candidate;
    }

    private static List<string> NormalizeLines(string text)
    {
        return text
            .Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => CleanupRegex.Replace(l, " ").Trim().Trim(EdgePunctuation).Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct()
            .ToList();
    }

    private string DetermineTitle(List<string> lines)
    {
        var titleCandidates = lines
            .Where(line => !ContainsAuthorToken(line))
            .ToList();

        return (titleCandidates.Count > 0 ? titleCandidates : lines)
            .OrderByDescending(l => l.Length)
            .First();
    }

    private string DetermineAuthor(List<string> lines, string title)
    {
        string authorLine = FindAuthorLine(lines, title);
        return string.IsNullOrEmpty(authorLine) ? string.Empty : ToTitleCase(authorLine);
    }

    private double CalculateConfidence(string title, string author, double ocrConfidence)
    {
        double confidence = _options.BaseConfidence;

        if (!string.IsNullOrEmpty(title))
        {
            confidence += Math.Min(0.4, title.Length / 50d);
        }

        if (!string.IsNullOrEmpty(author))
        {
            confidence += 0.2;
        }

        if (ocrConfidence > 0)
        {
            confidence = (confidence + ocrConfidence) / 2;
        }

        return Math.Clamp(confidence, 0, 0.99);
    }

    private static void AddAlternativeTitleNotes(BookCandidate candidate, string probableTitle, List<string> lines)
    {
        if (lines.Count <= 1)
        {
            return;
        }

        var best = Process.ExtractTop(probableTitle, lines, limit: 3).ToList();
        if (best.Count > 1 && best[1].Score > 90 && best[1].Value != probableTitle)
        {
            candidate.Notes.Add($"Alternative title candidate: {best[1].Value}");
        }
    }

    private string FindAuthorLine(List<string> lines, string title)
    {
        foreach (string line in lines)
        {
            string? token = FindAuthorToken(line);
            if (token is not null)
            {
                string author = line[(line.IndexOf(token, StringComparison.OrdinalIgnoreCase) + token.Length)..].Trim();
                if (!string.IsNullOrEmpty(author))
                {
                    return author;
                }
            }
        }

        string? probableAuthor = lines
            .Where(line => !string.Equals(line, title, StringComparison.OrdinalIgnoreCase))
            .Where(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 4)
            .OrderBy(l => l.Length)
            .FirstOrDefault(l => l.Any(char.IsLetter) && l.Split(' ').All(w => w.Length <= 12));

        return probableAuthor ?? string.Empty;
    }

    private bool ContainsAuthorToken(string line) => FindAuthorToken(line) is not null;

    private string? FindAuthorToken(string line)
    {
        return _options.CommonAuthorTokens
            .OrderByDescending(token => token.Length)
            .FirstOrDefault(token =>
                line.StartsWith(token, StringComparison.OrdinalIgnoreCase)
                && (line.Length == token.Length || !char.IsLetterOrDigit(line[token.Length])));
    }

    private static string ToTitleCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
    }
}
