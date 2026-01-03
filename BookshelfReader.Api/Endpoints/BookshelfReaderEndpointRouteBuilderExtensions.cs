using System;
using System.Collections.Generic;
using BookshelfReader.Core.Abstractions;
using BookshelfReader.Core.Models;
using BookshelfReader.Core.Options;
using BookshelfReader.Api.Validation;
using BookshelfReader.DependencyInjection.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

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
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
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

    private static async Task<Results<Ok<IEnumerable<BookMetadata>>, ProblemHttpResult, ValidationProblem>> LookupAsync(
        string name,
        IBookLookupService lookupService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CreateValidationProblem("name", "Query 'name' is required.");
        }

        var result = await lookupService.LookupAsync(name, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return TypedResults.Problem(
                title: "Book lookup failed",
                detail: result.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return TypedResults.Ok(result.Books);
    }

    private static async Task<Results<Ok<ParseResult>, ValidationProblem>> ParseAsync(
        HttpRequest request,
        IBookshelfProcessingService processor,
        IImageUploadRequestHandler uploadRequestHandler,
        CancellationToken cancellationToken)
    {
        var preparationResult = await uploadRequestHandler.PrepareAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!preparationResult.IsSuccess)
        {
            return preparationResult.Problem!;
        }

        await using var imageStream = preparationResult.Upload!.Stream;

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

    private static ValidationProblem CreateValidationProblem(string key, string message)
    {
        var errors = new Dictionary<string, string[]> { [key] = new[] { message } };
        return TypedResults.ValidationProblem(errors);
    }
}
