# BookshelfReader

BookshelfReader provides dependency injection helpers and API endpoint mappings for wiring a bookshelf parsing pipeline (vision spine reading, genre classification, and Open Library lookup) into an existing ASP.NET Core application. Use the `BookshelfReader` NuGet package; you do not need to run the included API host by itself. A runnable reference host lives in `BookshelfReader.Host` for local testing only and is not packaged. Only the `BookshelfReader` project is marked packable so `dotnet pack` emits just the NuGet payload.

Uploaded photos are read by sending them to a vision model. The provider is pluggable so you can use whichever vision API key you already have - Anthropic Claude (the default, via the [Messages API](https://docs.anthropic.com/)), OpenAI (Chat Completions, vision-capable model), or Google Gemini (Generative Language API). Select one with `Vision:Provider`; Claude stays the default and its behavior is unchanged. Regardless of provider, images are processed in memory only - never written to disk - and are downscaled and re-encoded before they leave the server, which strips all EXIF metadata (including phone GPS coordinates).

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
   * `Vision`: `Provider` selects the vision backend - `Claude` (default), `OpenAI`, or `Gemini` (case-insensitive). Only the selected provider's section below is bound and validated, so you only need a key for the provider you choose. The host fails fast at startup if the selected provider has no key.
   * `ClaudeVision` (used when `Vision:Provider` is `Claude`, the default): `ApiKey` (falls back to the `ANTHROPIC_API_KEY` environment variable if unset), `BaseUrl` (default `https://api.anthropic.com/`, must be HTTPS), `Model` (default `claude-haiku-4-5`), `MaxTokens` (default `2048`), `TimeoutSeconds` (default `60`), and `MaxImageDimension` (default `1568`, images are downscaled so their longest edge does not exceed this many pixels before being sent to the model).
   * `OpenAIVision` (used when `Vision:Provider` is `OpenAI`): `ApiKey` (falls back to `OPENAI_API_KEY`), `BaseUrl` (default `https://api.openai.com/`, must be HTTPS), `Model` (default `gpt-4o-mini`, a current low-cost vision-capable chat model), `MaxTokens` (default `2048`), `TimeoutSeconds` (default `60`), and `MaxImageDimension` (default `1568`). The image is sent as a base64 data URL and JSON mode is requested.
   * `GeminiVision` (used when `Vision:Provider` is `Gemini`): `ApiKey` (falls back to `GEMINI_API_KEY`), `BaseUrl` (default `https://generativelanguage.googleapis.com/`, must be HTTPS), `Model` (default `gemini-2.0-flash`, a current low-cost multimodal model), `MaxTokens` (default `2048`), `TimeoutSeconds` (default `60`), and `MaxImageDimension` (default `1568`). The image is sent as inline base64 data and JSON output is requested.
   * Optional: `OpenLibrary:BaseUrl` (must be HTTPS) and `OpenLibrary:UserAgent` (sent to Open Library; defaults to a BookshelfReader identifier).

For whichever provider you select, create a dedicated console workspace and API key for this integration (with its own spend limit) so usage and billing stay isolated. At Claude `claude-haiku-4-5` pricing, a typical bookshelf photo costs roughly $0.003-$0.005 per request; OpenAI `gpt-4o-mini` and Gemini `gemini-2.0-flash` are similarly inexpensive vision models, though exact costs vary by provider and image size.

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
