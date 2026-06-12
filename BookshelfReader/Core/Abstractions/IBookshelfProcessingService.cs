using BookshelfReader.Core.Models;

namespace BookshelfReader.Core.Abstractions;

public interface IBookshelfProcessingService
{
    Task<ParseResult> ProcessAsync(Stream imageStream, CancellationToken cancellationToken = default);
}
