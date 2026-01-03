using System.Net.Http.Json;
using BookshelfReader.Core.Abstractions;
using BookshelfReader.Core.Models;
using BookshelfReader.Core.Models.External;
using Microsoft.Extensions.Logging;

namespace BookshelfReader.Infrastructure.Lookup;

public sealed class OpenLibraryLookupService : IBookLookupService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenLibraryLookupService> _logger;

    public OpenLibraryLookupService(HttpClient httpClient, ILogger<OpenLibraryLookupService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BookLookupResult> LookupAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BookLookupResult.Success(Array.Empty<BookMetadata>());
        }

        try
        {
            var response = await _httpClient.GetFromJsonAsync<OpenLibrarySearchResult>(
                $"search.json?title={Uri.EscapeDataString(query)}",
                cancellationToken);

            if (response?.Docs is null || response.Docs.Count == 0)
            {
                return BookLookupResult.Success(Array.Empty<BookMetadata>());
            }

            var books = response.Docs
                .OrderByDescending(d => d.FirstPublishYear ?? 0)
                .Take(5)
                .Select(d => new BookMetadata
                {
                    Title = d.Title ?? string.Empty,
                    Author = d.AuthorName?.FirstOrDefault(),
                    PublishYear = d.FirstPublishYear,
                    Isbn = d.Isbn?.FirstOrDefault(),
                    CoverUrl = d.CoverId.HasValue
                        ? $"https://covers.openlibrary.org/b/id/{d.CoverId}-M.jpg"
                        : null
                })
                .ToArray();

            return BookLookupResult.Success(books);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open Library lookup failed for query '{Query}'", query);
            return BookLookupResult.Failure("Unable to reach the book lookup service. Please try again later.");
        }
    }
}
