using BookshelfReader.Core.Models;

namespace BookshelfReader.Core.Abstractions;

public interface IBookSegmentationService
{
    Task<IReadOnlyList<BookSegment>> SegmentAsync(Stream imageStream, CancellationToken cancellationToken = default);
}
