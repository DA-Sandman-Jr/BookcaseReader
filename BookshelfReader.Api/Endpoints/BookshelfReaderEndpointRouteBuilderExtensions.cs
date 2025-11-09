using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using BookshelfReader.DependencyInjection.Authentication;
using BookshelfReader.Core.Abstractions;
using BookshelfReader.Core.Models;
using BookshelfReader.Core.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace BookshelfReader.Api.Endpoints;

public static class BookshelfReaderEndpointRouteBuilderExtensions
{
    private const string ApiBasePath = "/api";
    private const string BooksTag = "Books";
    private const string BookshelfTag = "Bookshelf";

    public static RouteGroupBuilder MapBookshelfReaderApi(this IEndpointRouteBuilder app)
    {
        var apiGroup = app.MapGroup(ApiBasePath).WithOpenApi();

        apiGroup.MapGet("/books/lookup", LookupAsync)
            .WithName("LookupBooks")
            .WithTags(BooksTag)
            .Produces<IEnumerable<BookMetadata>>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        var apiKeyOptions = app.ServiceProvider.GetRequiredService<IOptions<ApiKeyAuthenticationOptions>>().Value;

        var parseEndpoint = apiGroup.MapPost("/bookshelf/parse", ParseAsync)
            .Accepts<IFormFile>("multipart/form-data")
            .WithName("ParseBookshelf")
            .WithTags(BookshelfTag)
            .Produces<ParseResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        if (apiKeyOptions.RequireApiKey)
        {
            parseEndpoint.RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = ApiKeyAuthenticationDefaults.AuthenticationScheme
            });
        }

        return apiGroup;
    }

    private static async Task<Results<Ok<IEnumerable<BookMetadata>>, ValidationProblem>> LookupAsync(
        string name,
        IBookLookupService lookupService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CreateValidationProblem("name", "Query 'name' is required.");
        }

        var books = await lookupService.LookupAsync(name, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(books);
    }

    private static async Task<Results<Ok<ParseResult>, ValidationProblem>> ParseAsync(
        HttpRequest request,
        IBookshelfProcessingService processor,
        IOptions<UploadsOptions> uploadsOptions,
        IOptions<SegmentationOptions> segmentationOptions,
        CancellationToken cancellationToken)
    {
        var uploadValidation = await ValidateImageUploadAsync(request, uploadsOptions.Value, cancellationToken)
            .ConfigureAwait(false);
        if (!uploadValidation.IsSuccess)
        {
            return uploadValidation.Problem!;
        }

        var file = uploadValidation.File!;
        var canonicalContentType = uploadValidation.CanonicalContentType!;

        await using var imageStream = file.OpenReadStream(uploadsOptions.Value.MaxBytes);

        var signatureValidation = await ValidateImageSignatureAsync(imageStream, canonicalContentType, cancellationToken)
            .ConfigureAwait(false);
        if (signatureValidation is not null)
        {
            return signatureValidation;
        }

        var metadataValidation = ValidateImageMetadata(imageStream, segmentationOptions.Value);
        if (metadataValidation is not null)
        {
            return metadataValidation;
        }

        try
        {
            var result = await processor.ProcessAsync(imageStream, cancellationToken).ConfigureAwait(false);
            return TypedResults.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return CreateValidationProblem("image", ex.Message);
        }
    }

    private static async Task<UploadValidationResult> ValidateImageUploadAsync(
        HttpRequest request,
        UploadsOptions options,
        CancellationToken cancellationToken)
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

        if (file.Length > options.MaxBytes)
        {
            return UploadValidationResult.Failure("image",
                $"Uploaded image exceeds the configured limit of {options.MaxBytes} bytes.");
        }

        if (!UploadsOptions.TryGetCanonicalContentType(file.ContentType, out var canonicalContentType)
            || !options.AllowedContentTypes.Contains(canonicalContentType))
        {
            return UploadValidationResult.Failure("image", "Unsupported image content type.");
        }

        return UploadValidationResult.Success(file, canonicalContentType);
    }

    private static async Task<ValidationProblem?> ValidateImageSignatureAsync(
        Stream imageStream,
        string canonicalContentType,
        CancellationToken cancellationToken)
    {
        imageStream.Position = 0;
        if (!await IsSupportedImageAsync(imageStream, canonicalContentType, cancellationToken).ConfigureAwait(false))
        {
            return CreateValidationProblem("image", "Uploaded image file content does not match the declared type.");
        }

        return null;
    }

    private static ValidationProblem? ValidateImageMetadata(Stream imageStream, SegmentationOptions segmentation)
    {
        imageStream.Position = 0;
        try
        {
            var imageInfo = Image.Identify(imageStream);
            if (imageInfo is null)
            {
                return CreateValidationProblem("image", "Unable to read the uploaded image metadata.");
            }

            var pixelCount = (long)imageInfo.Width * imageInfo.Height;
            if (pixelCount > segmentation.MaxImagePixels)
            {
                return CreateValidationProblem("image",
                    $"Uploaded image has {pixelCount:N0} pixels which exceeds the configured limit of {segmentation.MaxImagePixels:N0}.");
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
        finally
        {
            imageStream.Position = 0;
        }

        return null;
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
            stream.Position = 0;
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, signatureSpan.Length), cancellationToken)
                .ConfigureAwait(false);
            stream.Position = 0;

            if (bytesRead < signatureSpan.Length)
            {
                return false;
            }

            for (var i = 0; i < signatureSpan.Length; i++)
            {
                if (buffer[i] != signatureSpan[i])
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static ValidationProblem CreateValidationProblem(string key, string message)
    {
        var errors = new Dictionary<string, string[]> { [key] = new[] { message } };
        return TypedResults.ValidationProblem(errors);
    }

    private sealed record UploadValidationResult(IFormFile? File, string? CanonicalContentType, ValidationProblem? Problem)
    {
        public bool IsSuccess => File is not null && Problem is null;

        public static UploadValidationResult Success(IFormFile file, string canonicalContentType) =>
            new(file, canonicalContentType, null);

        public static UploadValidationResult Failure(string key, string message) =>
            new(null, null, CreateValidationProblem(key, message));
    }
}
