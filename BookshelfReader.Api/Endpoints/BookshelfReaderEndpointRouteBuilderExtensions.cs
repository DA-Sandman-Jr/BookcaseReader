using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using BookshelfReader.Api.Authentication;
using BookshelfReader.Core.Abstractions;
using BookshelfReader.Core.Models;
using BookshelfReader.Core.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
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

        apiGroup.MapPost("/bookshelf/parse", ParseAsync)
            .Accepts<IFormFile>("multipart/form-data")
            .WithName("ParseBookshelf")
            .WithTags(BookshelfTag)
            .Produces<ParseResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem()
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = ApiKeyAuthenticationDefaults.AuthenticationScheme
            });

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
        if (!request.HasFormContentType)
        {
            return CreateValidationProblem("Content-Type", "multipart/form-data content type required.");
        }

        var options = uploadsOptions.Value;
        var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var file = form.Files.GetFile("image");

        if (file is null)
        {
            return CreateValidationProblem("image", "Form field 'image' is required.");
        }

        if (file.Length == 0)
        {
            return CreateValidationProblem("image", "Uploaded image is empty.");
        }

        if (file.Length > options.MaxBytes)
        {
            return CreateValidationProblem("image", $"Uploaded image exceeds the configured limit of {options.MaxBytes} bytes.");
        }

        var contentType = file.ContentType;
        if (!UploadsOptions.TryGetCanonicalContentType(contentType, out var canonicalContentType)
            || !options.AllowedContentTypes.Contains(canonicalContentType))
        {
            return CreateValidationProblem("image", "Unsupported image content type.");
        }

        await using var imageStream = file.OpenReadStream(options.MaxBytes);

        if (!await IsSupportedImageAsync(imageStream, canonicalContentType, cancellationToken).ConfigureAwait(false))
        {
            return CreateValidationProblem("image", "Uploaded image file content does not match the declared type.");
        }

        var segmentation = segmentationOptions.Value;
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
}
