# BookshelfReader

BookshelfReader provides dependency injection helpers and API endpoint mappings for wiring a bookshelf parsing pipeline (Claude vision, genre classification, and Open Library lookup) into an existing ASP.NET Core application. Use the `BookshelfReader` NuGet package; you do not need to run the included API host by itself. A runnable reference host lives in `BookshelfReader.Host` for local testing only and is not packaged. Only the `BookshelfReader` project is marked packable so `dotnet pack` emits just the NuGet payload.

Uploaded photos are read by sending them to the [Anthropic Messages API](https://docs.anthropic.com/) (Claude vision). Images are processed in memory only - never written to disk - and are downscaled and re-encoded before they leave the server, which strips all EXIF metadata (including phone GPS coordinates). Anthropic does not train on API inputs.

## Consuming the NuGet package

1. Reference the `BookshelfReader` package from your ASP.NET Core project.
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
   * `ClaudeVision`: `ApiKey` (falls back to the `ANTHROPIC_API_KEY` environment variable if unset - required, the host fails fast at startup without one), `BaseUrl` (default `https://api.anthropic.com/`, must be HTTPS), `Model` (default `claude-haiku-4-5`), `MaxTokens` (default `2048`), `TimeoutSeconds` (default `60`), and `MaxImageDimension` (default `1568`, images are downscaled so their longest edge does not exceed this many pixels before being sent to Claude).
   * Optional: `OpenLibrary:BaseUrl` (must be HTTPS) and `OpenLibrary:UserAgent` (sent to Open Library; defaults to a BookshelfReader identifier).

Create a dedicated Anthropic Console workspace and API key for this integration (with its own spend limit) so usage and billing stay isolated from other Claude usage. At `claude-haiku-4-5` pricing, a typical bookshelf photo costs roughly $0.003-$0.005 per request.

## Packing for nuget.org (testing)

```bash
dotnet restore
dotnet pack BookshelfReader/BookshelfReader.csproj \
  -c Release \
  -p:PackageVersion=3.0.0-beta1 \
  -p:IncludeSymbols=true \
  -p:SymbolPackageFormat=snupkg
# Equivalent solution-wide pack (only the packable project emits a package)
# dotnet pack BookshelfReader.sln -c Release
```

Publish the resulting `.nupkg` (and optional `.snupkg`) artifact to nuget.org with your API key. Mark prerelease versions with a suffix such as `-beta1`.

## Tests

```bash
dotnet test
```

Tests cover the DI wiring, option validation, and pipeline components to ensure the NuGet surface behaves as expected.
