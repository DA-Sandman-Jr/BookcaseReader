using BookshelfReader.Core.Models;

namespace BookshelfReader.Core.Abstractions;

public interface IBookLookupService
{
    Task<IEnumerable<BookMetadata>> LookupAsync(string query, CancellationToken cancellationToken = default);
}
