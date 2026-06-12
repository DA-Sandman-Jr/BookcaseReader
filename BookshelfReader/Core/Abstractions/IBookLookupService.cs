using BookshelfReader.Core.Models;

namespace BookshelfReader.Core.Abstractions;

public interface IBookLookupService
{
    Task<BookLookupResult> LookupAsync(string query, CancellationToken cancellationToken = default);
}
