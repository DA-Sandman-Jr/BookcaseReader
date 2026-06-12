using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BookshelfReader.Api.Validation;

public interface IImageUploadValidator
{
    Task<UploadValidationResult> ValidateImageUploadAsync(HttpRequest request, CancellationToken cancellationToken);

    Task<ValidationProblem?> ValidateImageSignatureAsync(
        Stream imageStream,
        string canonicalContentType,
        CancellationToken cancellationToken);

    ValidationProblem? ValidateImageMetadata(Stream imageStream);
}
