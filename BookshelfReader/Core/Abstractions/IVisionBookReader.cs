using BookshelfReader.Core.Models;

namespace BookshelfReader.Core.Abstractions;

public interface IVisionBookReader
{
    Task<VisionReadResult> ReadAsync(byte[] imageData, CancellationToken cancellationToken = default);
}
