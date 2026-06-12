using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BookshelfReader.Api.Validation;

public interface IImageUploadRequestHandler
{
    Task<UploadProcessingResult> PrepareAsync(HttpRequest request, CancellationToken cancellationToken);
}

public sealed record UploadProcessingResult(ValidatedImageUpload? Upload, ValidationProblem? Problem)
{
    public bool IsSuccess => Upload is not null && Problem is null;
}

public sealed record ValidatedImageUpload(Stream Stream, string CanonicalContentType, long MaxBytes);
