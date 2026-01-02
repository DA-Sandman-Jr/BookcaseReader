using System.Buffers;
using System.Collections.Generic;
using BookshelfReader.Core.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace BookshelfReader.Api.Validation;

public sealed class ImageUploadValidator : IImageUploadValidator
{
    private readonly UploadsOptions _uploadsOptions;
    private readonly SegmentationOptions _segmentationOptions;

    public ImageUploadValidator(IOptions<UploadsOptions> uploadsOptions, IOptions<SegmentationOptions> segmentationOptions)
    {
        _uploadsOptions = uploadsOptions.Value;
        _segmentationOptions = segmentationOptions.Value;
    }

    public async Task<UploadValidationResult> ValidateImageUploadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return UploadValidationResult.Failure("Content-Type", "multipart/form-data content type required.");
        }

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return UploadValidationResult.Failure("image", "Unable to read the uploaded form data.");
        }

        var file = form.Files.GetFile("image");
        if (file is null)
        {
            return UploadValidationResult.Failure("image", "Form field 'image' is required.");
        }

        if (file.Length == 0)
        {
            return UploadValidationResult.Failure("image", "Uploaded image is empty.");
        }

        if (file.Length > _uploadsOptions.MaxBytes)
        {
            return UploadValidationResult.Failure("image",
                $"Uploaded image exceeds the configured limit of {_uploadsOptions.MaxBytes} bytes.");
        }

        if (!UploadsOptions.TryGetCanonicalContentType(file.ContentType, out var canonicalContentType)
            || !_uploadsOptions.AllowedContentTypes.Contains(canonicalContentType))
        {
            return UploadValidationResult.Failure("image", "Unsupported image content type.");
        }

        return UploadValidationResult.Success(file, canonicalContentType, _uploadsOptions.MaxBytes);
    }

    public async Task<ValidationProblem?> ValidateImageSignatureAsync(
        Stream imageStream,
        string canonicalContentType,
        CancellationToken cancellationToken)
    {
        return await WithStreamRewindAsync(imageStream, async () =>
        {
            var isSupported = await IsSupportedImageAsync(imageStream, canonicalContentType, cancellationToken)
                .ConfigureAwait(false);

            return isSupported
                ? null
                : CreateValidationProblem("image", "Uploaded image file content does not match the declared type.");
        }).ConfigureAwait(false);
    }

    public ValidationProblem? ValidateImageMetadata(Stream imageStream)
    {
        return WithStreamRewind(imageStream, () =>
        {
            try
            {
                var imageInfo = Image.Identify(imageStream);
                if (imageInfo is null)
                {
                    return CreateValidationProblem("image", "Unable to read the uploaded image metadata.");
                }

                var pixelCount = (long)imageInfo.Width * imageInfo.Height;
                if (pixelCount > _segmentationOptions.MaxImagePixels)
                {
                    return CreateValidationProblem("image",
                        $"Uploaded image has {pixelCount:N0} pixels which exceeds the configured limit of {_segmentationOptions.MaxImagePixels:N0}.");
                }
            }
            catch (UnknownImageFormatException)
            {
                return CreateValidationProblem("image", "Uploaded image is not in a supported format.");
            }
            catch (InvalidImageContentException)
            {
                return CreateValidationProblem("image", "Uploaded image could not be processed.");
            }

            return null;
        });
    }

    private static async Task<bool> IsSupportedImageAsync(Stream stream, string contentType, CancellationToken cancellationToken)
    {
        if (!UploadsOptions.TryGetImageSignature(contentType, out var signature))
        {
            return false;
        }

        var signatureSpan = signature.Span;
        var buffer = ArrayPool<byte>.Shared.Rent(signatureSpan.Length);

        try
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, signatureSpan.Length), cancellationToken)
                .ConfigureAwait(false);

            if (bytesRead < signatureSpan.Length)
            {
                return false;
            }

            return buffer.AsSpan(0, signatureSpan.Length).SequenceEqual(signatureSpan);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    internal static ValidationProblem CreateValidationProblem(string key, string message)
    {
        var errors = new Dictionary<string, string[]> { [key] = new[] { message } };
        return TypedResults.ValidationProblem(errors);
    }

    private static async Task<T> WithStreamRewindAsync<T>(Stream stream, Func<Task<T>> action)
    {
        stream.Position = 0;
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            stream.Position = 0;
        }
    }

    private static T WithStreamRewind<T>(Stream stream, Func<T> action)
    {
        stream.Position = 0;
        try
        {
            return action();
        }
        finally
        {
            stream.Position = 0;
        }
    }
}

public sealed record UploadValidationResult(
    IFormFile? File,
    string? CanonicalContentType,
    long MaxBytes,
    ValidationProblem? Problem)
{
    public bool IsSuccess => File is not null && Problem is null;

    public static UploadValidationResult Success(IFormFile file, string canonicalContentType, long maxBytes) =>
        new(file, canonicalContentType, maxBytes, null);

    public static UploadValidationResult Failure(string key, string message) =>
        new(null, null, 0, ImageUploadValidator.CreateValidationProblem(key, message));
}
