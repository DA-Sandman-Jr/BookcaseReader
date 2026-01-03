using System.Collections.Generic;

namespace BookshelfReader.Core.Models;

public sealed record BookLookupResult(IEnumerable<BookMetadata> Books, string? ErrorMessage)
{
    public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage);

    public static BookLookupResult Success(IEnumerable<BookMetadata> books) => new(books, null);

    public static BookLookupResult Failure(string error) => new(Array.Empty<BookMetadata>(), error);
}
