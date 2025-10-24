using BookshelfReader.Core.Models;

namespace BookshelfReader.Core.Abstractions;

public interface IOcrService
{
    Task<OcrResult> RecognizeAsync(byte[] imageData, CancellationToken cancellationToken = default);
}
