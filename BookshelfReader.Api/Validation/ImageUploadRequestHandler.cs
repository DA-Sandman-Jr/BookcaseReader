using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BookshelfReader.Api.Validation;

public sealed class ImageUploadRequestHandler : IImageUploadRequestHandler
{
    private readonly IImageUploadValidator _validator;

    public ImageUploadRequestHandler(IImageUploadValidator validator)
    {
        _validator = validator;
    }

    public async Task<UploadProcessingResult> PrepareAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        UploadValidationResult uploadValidation = await _validator.ValidateImageUploadAsync(request, cancellationToken)
            .ConfigureAwait(false);
        if (!uploadValidation.IsSuccess)
        {
            return new UploadProcessingResult(null, uploadValidation.Problem);
        }

        IFormFile file = uploadValidation.File!;
        string canonicalContentType = uploadValidation.CanonicalContentType!;

        Stream seekableStream = await CreateSeekableStreamAsync(file, uploadValidation.MaxBytes, cancellationToken)
            .ConfigureAwait(false);

        ValidationProblem? signatureValidation = await _validator.ValidateImageSignatureAsync(
                seekableStream,
                canonicalContentType,
                cancellationToken)
            .ConfigureAwait(false);
        if (signatureValidation is not null)
        {
            await seekableStream.DisposeAsync().ConfigureAwait(false);
            return new UploadProcessingResult(null, signatureValidation);
        }

        ValidationProblem? metadataValidation = _validator.ValidateImageMetadata(seekableStream);
        if (metadataValidation is not null)
        {
            await seekableStream.DisposeAsync().ConfigureAwait(false);
            return new UploadProcessingResult(null, metadataValidation);
        }

        var upload = new ValidatedImageUpload(seekableStream, canonicalContentType, uploadValidation.MaxBytes);
        return new UploadProcessingResult(upload, null);
    }

    private static async Task<Stream> CreateSeekableStreamAsync(IFormFile file, long maxBytes, CancellationToken cancellationToken)
    {
        Stream stream = file.OpenReadStream();

        if (stream.CanSeek)
        {
            stream.Position = 0;
            return stream;
        }

        var buffer = new MemoryStream(capacity: (int)Math.Min(maxBytes, int.MaxValue));
        await using (stream.ConfigureAwait(false))
        {
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        buffer.Position = 0;
        return buffer;
    }
}
