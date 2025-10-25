# BookshelfReader

BookshelfReader is a .NET 8 Web API that turns a raw bookshelf photo into structured book metadata. The service segments books using OpenCV, performs OCR with Tesseract, parses probable titles/authors, classifies genres using keyword rules, and enriches metadata via the public Open Library catalog.

## Solution structure

```
BookshelfReader.sln
├── BookshelfReader.Api             # Minimal APIs, dependency injection, configuration
├── BookshelfReader.Core            # Domain models, options, abstraction interfaces
├── BookshelfReader.Infrastructure  # OpenCV segmentation, Tesseract OCR, parsing, genre classification, lookups
└── BookshelfReader.Tests           # xUnit test suite (FluentAssertions + MockHttp)
```

## Prerequisites

* .NET 8 SDK
* Native dependencies for [OpenCvSharp4](https://github.com/shimat/opencvsharp) (the `OpenCvSharp4.runtime.anycpu` package loads the unmanaged binaries automatically on supported platforms)
* [Tesseract OCR language data](https://github.com/tesseract-ocr/tessdata). By default the API looks for `tessdata` inside the application directory; override via `Ocr:Tesseract:DataPath` in configuration.

## Configuration

`BookshelfReader.Api/appsettings.json` contains sensible defaults:

* **Uploads** – maximum upload size (10 MB) and allowed MIME types.
* **OpenLibrary** – base URL for outbound metadata lookups.
* **Ocr:Tesseract** – path to tessdata, language(s), and max OCR concurrency.
* **Segmentation / Parsing** – heuristics for contour filtering and text parsing.

Override any value via environment variables or user secrets (e.g. `ASPNETCORE_ENVIRONMENT=Development`).

## Running the API

```
dotnet restore
cd BookshelfReader.Api
dotnet run
```

Swagger UI is available at `https://localhost:5001/swagger`.

## Adding the endpoints to an existing ASP.NET Core app

If you already have an ASP.NET Core application and want to expose the bookshelf parsing endpoints from the same host:

1. Reference the `BookshelfReader.Api`, `BookshelfReader.Core`, and `BookshelfReader.Infrastructure` projects from your web application.
2. Configure services and authentication in `Program.cs` (add `using BookshelfReader.Api.Extensions;` and `using BookshelfReader.Api.Authentication;`):

   ```csharp
   builder.Services.AddBookshelfReader(builder.Configuration);

   builder.Services
       .AddAuthentication(options =>
       {
           options.DefaultScheme = ApiKeyAuthenticationDefaults.AuthenticationScheme;
           options.DefaultAuthenticateScheme = ApiKeyAuthenticationDefaults.AuthenticationScheme;
           options.DefaultChallengeScheme = ApiKeyAuthenticationDefaults.AuthenticationScheme;
       })
       .AddBookshelfReaderApiKey();

   builder.Services.AddAuthorization();
   ```

3. Map the endpoints alongside your existing routes:

   ```csharp
   app.MapBookshelfReaderApi();
   ```

4. Copy the `Authentication`, `Uploads`, and other relevant sections from `BookshelfReader.Api/appsettings.json` into your application's configuration so the options binding succeeds. At least one non-empty API key must be provided before startup.

The `AddBookshelfReader` extension wires up the OCR/segmentation pipeline, validation rules, HTTP client for metadata lookup, and multipart upload limits. The `MapBookshelfReaderApi` extension registers the `/api/books/lookup` and `/api/bookshelf/parse` routes so you can expose them from any existing minimal API or MVC host.

### Sample requests

Parse a bookshelf image:

```
curl -X POST "https://localhost:5001/api/bookshelf/parse" \
  -H "accept: application/json" \
  -F "image=@/path/to/bookshelf.jpg"
```

Lookup metadata via Open Library:

```
curl "https://localhost:5001/api/books/lookup?name=the%20hobbit"
```

## Tests

```
dotnet test
```

The test suite currently covers the Open Library integration. Additional tests can be added around parsing, segmentation, and orchestration using golden images or fixtures.

## Notes

* Upload validation rejects non-image content types and files over the configured size limit.
* Segmentation uses grayscale → blur → Canny → morphology to find spine contours, deskews via `minAreaRect`, and crops each book before OCR.
* OCR tries 0°, 90°, and 270° rotations to improve recognition when spines are vertical.
* Genre classification is rule-based today but the `IGenreClassifier` abstraction makes it easy to plug in ML/LLM classifiers later.
* Lookup timeouts or HTTP failures are logged and surfaced as empty results rather than 5xx errors.

## Dependency updates

* 2025-10-25: Bumped OpenCVSharp4, Tesseract, Microsoft.Extensions.*, and test packages (xUnit, FluentAssertions, coverlet, Microsoft.NET.Test.Sdk, MockHttp) to the latest stable releases for compatibility and security maintenance.
