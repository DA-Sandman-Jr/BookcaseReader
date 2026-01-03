using System;
using System.Linq;
using BookshelfReader.Core.Abstractions;
using BookshelfReader.Core.Options;
using BookshelfReader.DependencyInjection.Authentication;
using BookshelfReader.Infrastructure.Genres;
using BookshelfReader.Infrastructure.Lookup;
using BookshelfReader.Infrastructure.Ocr;
using BookshelfReader.Infrastructure.Parsing;
using BookshelfReader.Infrastructure.Processing;
using BookshelfReader.Infrastructure.Segmentation;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class BookshelfReaderServiceCollectionExtensions
{
    public static IServiceCollection AddBookshelfReader(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddProblemDetails();

        services.AddOptions<ApiKeyAuthenticationOptions>()
            .Bind(configuration.GetSection(ApiKeyAuthenticationOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.HeaderName), "API key header name must be configured.")
            .Validate(options => !options.RequireApiKey || options.ValidKeys is { Count: > 0 }
                    && options.ValidKeys.All(key => !string.IsNullOrWhiteSpace(key)),
                "At least one non-empty API key must be configured when API keys are required.")
            .ValidateOnStart();

        services.AddOptions<UploadsOptions>()
            .Bind(configuration.GetSection(UploadsOptions.SectionName))
            .Validate(options => options.MaxBytes is > 0 and <= 20 * 1024 * 1024,
                "Uploads:MaxBytes must be between 1 byte and 20 MB.")
            .Validate(options => options.AllowedContentTypes is { Count: > 0 }
                && options.AllowedContentTypes.All(UploadsOptions.IsSupportedContentType),
                $"Uploads:AllowedContentTypes must contain only supported image MIME types ({string.Join(", ", UploadsOptions.SupportedImageSignatures.Keys)}).")
            .ValidateOnStart();

        services.AddOptions<FormOptions>()
            .Configure<IOptions<UploadsOptions>>((formOptions, uploadOptions) =>
            {
                formOptions.MultipartBodyLengthLimit = uploadOptions.Value.MaxBytes;
            });

        services.Configure<TesseractOcrOptions>(configuration.GetSection(TesseractOcrOptions.SectionName));
        services.AddOptions<SegmentationOptions>()
            .Bind(configuration.GetSection(SegmentationOptions.SectionName))
            .Validate(options => options.MaxImagePixels is > 0 and <= 50_000_000,
                "Segmentation:MaxImagePixels must be between 1 and 50,000,000.")
            .ValidateOnStart();
        services.Configure<ParsingOptions>(configuration.GetSection(ParsingOptions.SectionName));

        services.AddSingleton<IBookSegmentationService, OpenCvBookSegmentationService>();
        services.AddSingleton<IOcrService, TesseractOcrService>();
        services.AddSingleton<IBookParsingService, BookParsingService>();
        services.AddSingleton<IGenreClassifier, KeywordGenreClassifier>();
        services.AddSingleton<IBookSegmentProcessor, BookSegmentProcessor>();
        services.AddSingleton<IBookshelfProcessingService, BookshelfProcessingService>();

        services.AddHttpClient<IBookLookupService, OpenLibraryLookupService>((_, client) =>
        {
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

        return services;
    }
}
