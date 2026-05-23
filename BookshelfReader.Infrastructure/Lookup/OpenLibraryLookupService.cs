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

    private const int MaxRetries = 3;

    public async Task<BookLookupResult> LookupAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BookLookupResult.Success(Array.Empty<BookMetadata>());
        }

        string requestUri = $"search.json?title={Uri.EscapeDataString(query)}";

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            if (attempt > 0)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogDebug("Retrying Open Library lookup (attempt {Attempt}/{Max}) after {Delay}s", attempt + 1, MaxRetries, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                OpenLibrarySearchResult? response = await _httpClient.GetFromJsonAsync<OpenLibrarySearchResult>(requestUri, cancellationToken).ConfigureAwait(false);

                if (response?.Docs is null || response.Docs.Count == 0)
                {
                    return BookLookupResult.Success(Array.Empty<BookMetadata>());
                }

                BookMetadata[] books = response.Docs
                    .OrderByDescending(d => d.FirstPublishYear ?? 0)
                    .Take(5)
                    .Select(d => new BookMetadata
                    {
                        Title = d.Title ?? string.Empty,
                        Author = d.AuthorName?.FirstOrDefault() ?? string.Empty,
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
                _logger.LogWarning(ex, "Open Library lookup failed for query '{Query}' (attempt {Attempt}/{Max})", query, attempt + 1, MaxRetries);
            }
        }

        return BookLookupResult.Failure("Unable to reach the book lookup service. Please try again later.");
    }
}
