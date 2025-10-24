using System.Collections.Generic;
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
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .WithName("ParseBookshelf")
            .WithTags(BookshelfTag)
            .Produces<ParseResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem();
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

        await using var imageStream = file.OpenReadStream();
        var result = await processor.ProcessAsync(imageStream, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(result);
    }

    private static ValidationProblem CreateValidationProblem(string key, string message)
    {
        var errors = new Dictionary<string, string[]> { [key] = new[] { message } };
        return TypedResults.ValidationProblem(errors);
    }
}
