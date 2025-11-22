# BookshelfReader

BookshelfReader provides dependency injection helpers for wiring the bookshelf parsing pipeline (segmentation, OCR, parsing, genre classification, and Open Library lookup) into an existing ASP.NET Core application. Use the NuGet package; you do not need to run the included API host by itself.

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
```

Publish the resulting `.nupkg` (and optional `.snupkg`) to nuget.org with your API key. Mark prerelease versions with a suffix such as `-beta1`.

## Tests

```bash
dotnet test
```

Tests cover the DI wiring, option validation, and pipeline components to ensure the NuGet surface behaves as expected.
