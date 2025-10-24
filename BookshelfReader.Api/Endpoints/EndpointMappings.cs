using System.Buffers;
using System.Collections.Generic;
using System.IO;
using BookshelfReader.Core.Abstractions;
using BookshelfReader.Core.Models;
using BookshelfReader.Core.Options;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BookshelfReader.Api.Endpoints;

internal static class EndpointMappings
{
    private const string ApiBasePath = "/api";
    private const string BooksTag = "Books";
    private const string BookshelfTag = "Bookshelf";

    internal static void MapApiEndpoints(this IEndpointRouteBuilder app)
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
            .RequireAuthorization();
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
        if (string.IsNullOrWhiteSpace(contentType) || !options.AllowedContentTypes.Contains(contentType))
        {
            return CreateValidationProblem("image", "Unsupported image content type.");
        }

        await using var imageStream = file.OpenReadStream(options.MaxBytes);

        if (!await IsSupportedImageAsync(imageStream, contentType, cancellationToken).ConfigureAwait(false))
        {
            return CreateValidationProblem("image", "Uploaded image file content does not match the declared type.");
        }

        var result = await processor.ProcessAsync(imageStream, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(result);
    }

    private static async Task<bool> IsSupportedImageAsync(Stream stream, string contentType, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(8);

        try
        {
            stream.Position = 0;
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            stream.Position = 0;

            return contentType switch
            {
                "image/jpeg" => bytesRead >= 3 && buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF,
                "image/png" => bytesRead >= 8
                    && buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47
                    && buffer[4] == 0x0D && buffer[5] == 0x0A && buffer[6] == 0x1A && buffer[7] == 0x0A,
                _ => false,
            };
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static ValidationProblem CreateValidationProblem(string key, string message)
    {
        var errors = new Dictionary<string, string[]> { [key] = new[] { message } };
        return TypedResults.ValidationProblem(errors);
    }
}
