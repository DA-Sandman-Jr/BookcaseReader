using BookshelfReader.Core.Abstractions;
using BookshelfReader.Core.Models;
using BookshelfReader.Core.Options;
using BookshelfReader.Infrastructure.Genres;
using BookshelfReader.Infrastructure.Lookup;
using BookshelfReader.Infrastructure.Ocr;
using BookshelfReader.Infrastructure.Parsing;
using BookshelfReader.Infrastructure.Processing;
using BookshelfReader.Infrastructure.Segmentation;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<UploadsOptions>(builder.Configuration.GetSection(UploadsOptions.SectionName));
builder.Services.Configure<TesseractOcrOptions>(builder.Configuration.GetSection(TesseractOcrOptions.SectionName));
builder.Services.Configure<SegmentationOptions>(builder.Configuration.GetSection(SegmentationOptions.SectionName));
builder.Services.Configure<ParsingOptions>(builder.Configuration.GetSection(ParsingOptions.SectionName));

builder.Services.AddSingleton<IBookSegmentationService, OpenCvBookSegmentationService>();
builder.Services.AddSingleton<IOcrService, TesseractOcrService>();
builder.Services.AddSingleton<IBookParsingService, BookParsingService>();
builder.Services.AddSingleton<IGenreClassifier, KeywordGenreClassifier>();
builder.Services.AddSingleton<IBookshelfProcessingService, BookshelfProcessingService>();

builder.Services.AddHttpClient<IBookLookupService, OpenLibraryLookupService>(client =>
{
    var baseUrl = builder.Configuration["OpenLibrary:BaseUrl"] ?? "https://openlibrary.org/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/api/books/lookup", async (string name, IBookLookupService lookupService, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.BadRequest(new { error = "Query 'name' is required." });
    }

    var books = await lookupService.LookupAsync(name, cancellationToken).ConfigureAwait(false);
    return Results.Ok(books);
}).WithName("LookupBooks").WithOpenApi();

app.MapPost("/api/bookshelf/parse", async (HttpRequest request, IBookshelfProcessingService processor, IOptions<UploadsOptions> uploadsOptions, CancellationToken cancellationToken) =>
{
    var options = uploadsOptions.Value;

    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "multipart/form-data content type required." });
    }

    var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
    var file = form.Files.GetFile("image");
    if (file is null)
    {
        return Results.BadRequest(new { error = "Form field 'image' is required." });
    }

    if (file.Length == 0)
    {
        return Results.BadRequest(new { error = "Uploaded image is empty." });
    }

    if (file.Length > options.MaxBytes)
    {
        return Results.BadRequest(new { error = $"Uploaded image exceeds the configured limit of {options.MaxBytes} bytes." });
    }

    if (!options.AllowedContentTypes.Contains(file.ContentType ?? string.Empty))
    {
        return Results.BadRequest(new { error = "Unsupported image content type." });
    }

    await using var imageStream = file.OpenReadStream();
    var result = await processor.ProcessAsync(imageStream, cancellationToken).ConfigureAwait(false);
    return Results.Ok(result);
}).DisableAntiforgery().Accepts<IFormFile>("multipart/form-data").Produces<ParseResult>(StatusCodes.Status200OK).WithName("ParseBookshelf").WithOpenApi();

app.Run();
