using System;
using System.Linq;
using System.Threading.Tasks;
using BookshelfReader.Api.Authentication;
using BookshelfReader.Api.Endpoints;
using BookshelfReader.Core.Abstractions;
using BookshelfReader.Core.Options;
using BookshelfReader.Infrastructure.Genres;
using BookshelfReader.Infrastructure.Lookup;
using BookshelfReader.Infrastructure.Ocr;
using BookshelfReader.Infrastructure.Parsing;
using BookshelfReader.Infrastructure.Processing;
using BookshelfReader.Infrastructure.Segmentation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();

builder.Services.AddOptions<ApiKeyAuthenticationOptions>()
    .Bind(builder.Configuration.GetSection(ApiKeyAuthenticationOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.HeaderName), "API key header name must be configured.")
    .Validate(options => options.ValidKeys is { Count: > 0 } && options.ValidKeys.All(key => !string.IsNullOrWhiteSpace(key)),
        "At least one non-empty API key must be configured.")
    .ValidateOnStart();

builder.Services.AddOptions<UploadsOptions>()
    .Bind(builder.Configuration.GetSection(UploadsOptions.SectionName))
    .Validate(options => options.MaxBytes is > 0 and <= 20 * 1024 * 1024,
        "Uploads:MaxBytes must be between 1 byte and 20 MB.")
    .Validate(options => options.AllowedContentTypes is { Count: > 0 }
        && options.AllowedContentTypes.All(UploadsOptions.IsSupportedContentType),
        $"Uploads:AllowedContentTypes must contain only supported image MIME types ({string.Join(", ", UploadsOptions.SupportedImageSignatures.Keys)}).")
    .ValidateOnStart();

builder.Services.AddOptions<FormOptions>()
    .Configure<IOptions<UploadsOptions>>((formOptions, uploadOptions) =>
    {
        formOptions.MultipartBodyLengthLimit = uploadOptions.Value.MaxBytes;
    });

builder.Services.Configure<TesseractOcrOptions>(builder.Configuration.GetSection(TesseractOcrOptions.SectionName));
builder.Services.Configure<SegmentationOptions>(builder.Configuration.GetSection(SegmentationOptions.SectionName));
builder.Services.Configure<ParsingOptions>(builder.Configuration.GetSection(ParsingOptions.SectionName));

builder.Services.AddAuthentication(ApiKeyAuthenticationDefaults.AuthenticationScheme)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationDefaults.AuthenticationScheme,
        _ => { });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<IBookSegmentationService, OpenCvBookSegmentationService>();
builder.Services.AddSingleton<IOcrService, TesseractOcrService>();
builder.Services.AddSingleton<IBookParsingService, BookParsingService>();
builder.Services.AddSingleton<IGenreClassifier, KeywordGenreClassifier>();
builder.Services.AddSingleton<IBookshelfProcessingService, BookshelfProcessingService>();

builder.Services.AddHttpClient<IBookLookupService, OpenLibraryLookupService>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["OpenLibrary:BaseUrl"] ?? "https://openlibrary.org/";

    if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
    {
        throw new InvalidOperationException("OpenLibrary:BaseUrl must be a valid absolute URI.");
    }

    if (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("OpenLibrary:BaseUrl must use HTTPS.");
    }

    client.BaseAddress = baseUri;
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cache-Control"] = "no-store";
        return Task.CompletedTask;
    });

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapApiEndpoints();

app.Run();
