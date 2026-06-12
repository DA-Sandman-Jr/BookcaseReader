using BookshelfReader.Core.Models;

namespace BookshelfReader.Core.Abstractions;

public interface IBookEnrichmentService
{
    Task EnrichAsync(IReadOnlyList<BookCandidate> candidates, CancellationToken cancellationToken = default);
}
