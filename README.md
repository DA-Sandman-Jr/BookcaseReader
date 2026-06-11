# BookshelfReader

BookshelfReader provides dependency injection helpers for wiring the bookshelf parsing pipeline (segmentation, OCR, parsing, genre classification, and Open Library lookup) into an existing ASP.NET Core application. Use the NuGet package; you do not need to run the included API host by itself. A runnable reference host lives in `BookshelfReader.Host` for local testing only and is not packaged. Only the `BookshelfReader.Extensions` and `BookshelfReader.Api` projects are marked packable so `dotnet pack` emits just the NuGet payloads.

## Consuming the NuGet package

1. Reference the `BookshelfReader.Extensions` package (and `BookshelfReader.Api` if you want the prebuilt endpoint mappings) from your ASP.NET Core project.
2. In `Program.cs` add:
   ```csharp
   using BookshelfReader.Api.Extensions;
   using BookshelfReader.Extensions;
   using BookshelfReader.Extensions.Authentication;

   builder.Services.AddBookshelfReader(builder.Configuration);

   // Required when you map the endpoints yourself: registers the image upload
   // validation/handling services that MapBookshelfReaderApi() depends on.
   builder.Services.AddBookshelfReaderApi();

   builder.Services
       .AddAuthentication(options =>
       {
           options.DefaultScheme = ApiKeyAuthenticationDefaults.AuthenticationScheme;
           options.DefaultAuthenticateScheme = ApiKeyAuthenticationDefaults.AuthenticationScheme;
           options.DefaultChallengeScheme = ApiKeyAuthenticationDefaults.AuthenticationScheme;
       })
       .AddBookshelfReaderApiKey();

   builder.Services.AddAuthorization();
   builder.Services.AddBookshelfReaderRateLimiting(builder.Configuration);

   app.UseRateLimiter();
   app.MapBookshelfReaderApi();
   ```
3. Provide configuration values (environment variables or `appsettings.json`):
   * `Authentication:ApiKey`: `HeaderName` (default `X-API-Key`), `RequireApiKey`, and `ValidKeys`.
   * `RateLimiting:Parse`: `Enabled` (default `false`), `PermitLimit` (default `10`), and `WindowSeconds` (default `60`). Enabling this policy also requires `builder.Services.AddBookshelfReaderRateLimiting(builder.Configuration);` during service registration and `app.UseRateLimiter();` before mapping the API endpoints.
   * `Uploads`: `MaxBytes` (1–20 MB) and `AllowedContentTypes` (JPEG/PNG).
   * `Enrichment`: `Enabled` (default `true`), `MaxConcurrentLookups` (1–16, default 4), and `MinMatchScore` (0–100, default 55). When enabled, `/api/bookshelf/parse` looks up each parsed title against Open Library and attaches the best match (title, author, year, ISBN, cover URL, subjects) to the candidate's `metadata` field, so callers get display-ready results from a single request.
   * Optional: `OpenLibrary:BaseUrl` (must be HTTPS), `OpenLibrary:UserAgent` (sent to Open Library; defaults to a BookshelfReader identifier), `Ocr:Tesseract`, `Segmentation`, and `Parsing` settings.

Embedders must provide OCR language data by placing `eng.traineddata` (or the languages they configure) in a tessdata directory and pointing `Ocr:Tesseract:DataPath` at it. `BookshelfReader.Host` downloads `eng.traineddata` from the [tessdata_fast repository](https://github.com/tesseract-ocr/tessdata_fast) automatically on first build, but package consumers need to provision that folder themselves.

## Packing for nuget.org (testing)

```bash
dotnet restore
dotnet pack BookshelfReader.Extensions/BookshelfReader.Extensions.csproj \
  -c Release \
  -p:PackageVersion=1.0.0-beta1 \
  -p:IncludeSymbols=true \
  -p:SymbolPackageFormat=snupkg
dotnet pack BookshelfReader.Api/BookshelfReader.Api.csproj \
  -c Release \
  -p:PackageVersion=1.0.0-beta1 \
  -p:IncludeSymbols=true \
  -p:SymbolPackageFormat=snupkg
# Equivalent solution-wide pack (only the packable projects emit packages)
# dotnet pack BookshelfReader.sln -c Release
```

Publish the resulting `.nupkg` (and optional `.snupkg`) artifacts from `BookshelfReader.Extensions` (and optionally `BookshelfReader.Api` for the endpoint mappings) to nuget.org with your API key. Mark prerelease versions with a suffix such as `-beta1`.

## Tests

```bash
dotnet test
```

Tests cover the DI wiring, option validation, and pipeline components to ensure the NuGet surface behaves as expected.
