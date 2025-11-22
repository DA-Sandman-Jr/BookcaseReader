# BookshelfReader

BookshelfReader provides dependency injection helpers for wiring the bookshelf parsing pipeline (segmentation, OCR, parsing, genre classification, and Open Library lookup) into an existing ASP.NET Core application. Use the NuGet package; you do not need to run the included API host by itself. A runnable reference host lives in `BookshelfReader.Host` for local testing only and is not packaged. Only the `BookshelfReader.DependencyInjection` and `BookshelfReader.Api` projects are marked packable so `dotnet pack` emits just the NuGet payloads.

## Consuming the NuGet package

1. Reference the `BookshelfReader.DependencyInjection` package (and `BookshelfReader.Api` if you want the prebuilt endpoint mappings) from your ASP.NET Core project.
2. In `Program.cs` add:
   ```csharp
   using BookshelfReader.Api.Extensions;
   using BookshelfReader.DependencyInjection.Authentication;

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

   app.MapBookshelfReaderApi();
   ```
3. Provide configuration values (environment variables or `appsettings.json`):
   * `Authentication:ApiKey`: `HeaderName` (default `X-API-Key`), `RequireApiKey`, and `ValidKeys`.
   * `Uploads`: `MaxBytes` (1–20 MB) and `AllowedContentTypes` (JPEG/PNG).
   * Optional: `OpenLibrary:BaseUrl` (must be HTTPS), `Ocr:Tesseract`, `Segmentation`, and `Parsing` settings.

## Packing for nuget.org (testing)

```bash
dotnet restore
dotnet pack BookshelfReader.DependencyInjection/BookshelfReader.DependencyInjection.csproj \
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

Publish the resulting `.nupkg` (and optional `.snupkg`) artifacts from `BookshelfReader.DependencyInjection` (and optionally `BookshelfReader.Api` for the endpoint mappings) to nuget.org with your API key. Mark prerelease versions with a suffix such as `-beta1`.

## Tests

```bash
dotnet test
```

Tests cover the DI wiring, option validation, and pipeline components to ensure the NuGet surface behaves as expected.
