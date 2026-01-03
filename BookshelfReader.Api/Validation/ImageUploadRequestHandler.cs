using System;
using Microsoft.AspNetCore.Http;

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
        var uploadValidation = await _validator.ValidateImageUploadAsync(request, cancellationToken)
            .ConfigureAwait(false);
        if (!uploadValidation.IsSuccess)
        {
            return new UploadProcessingResult(null, uploadValidation.Problem);
        }

        var file = uploadValidation.File!;
        var canonicalContentType = uploadValidation.CanonicalContentType!;

        var seekableStream = await CreateSeekableStreamAsync(file, uploadValidation.MaxBytes, cancellationToken)
            .ConfigureAwait(false);

        var signatureValidation = await _validator.ValidateImageSignatureAsync(
                seekableStream,
                canonicalContentType,
                cancellationToken)
            .ConfigureAwait(false);
        if (signatureValidation is not null)
        {
            await seekableStream.DisposeAsync().ConfigureAwait(false);
            return new UploadProcessingResult(null, signatureValidation);
        }

        var metadataValidation = _validator.ValidateImageMetadata(seekableStream);
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
        var stream = file.OpenReadStream(maxBytes);

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
