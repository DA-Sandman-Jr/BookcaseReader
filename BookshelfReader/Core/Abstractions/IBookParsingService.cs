using BookshelfReader.Core.Models;

namespace BookshelfReader.Core.Abstractions;

public interface IBookParsingService
{
    BookCandidate Parse(BookSegment segment, OcrResult ocr);
}
