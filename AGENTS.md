> **Auto-generated from `CLAUDE.md`** - edit the sibling `CLAUDE.md` instead. Changes made directly to this file will be overwritten on the next build by `Directory.Build.targets`.

# BookshelfReader - Agent Instructions

BookshelfReader provides .NET 8 dependency-injection helpers and optional API endpoint mappings for a bookshelf image parsing pipeline: upload validation, pluggable vision book reading (Claude, OpenAI, or Gemini), genre classification, and Open Library lookup with in-pipeline metadata enrichment.

## Commands

Restore dependencies:

```bash
dotnet restore BookshelfReader.sln
```

Build the solution:

```bash
dotnet build BookshelfReader.sln
```

Run tests:

```bash
dotnet test BookshelfReader.sln
```

Run the reference host for local testing:

```bash
dotnet run --project BookshelfReader.Host
```

Pack the NuGet payloads:

```bash
dotnet pack BookshelfReader.sln -c Release
```

## Architecture

- `BookshelfReader/` is the single packable project. It contains shared domain contracts and models (`Core/`), concrete pipeline implementations and external integrations such as image preprocessing, the vision book readers (Claude/OpenAI/Gemini, all behind `IVisionBookReader` in `Infrastructure/VisionLlm/`), genre classification, and Open Library lookup (`Infrastructure/`), service-registration helpers (`Extensions/`), and endpoint-mapping helpers and API surface (`Api/`). `dotnet pack` emits a single `BookshelfReader` NuGet package from this project.
- `BookshelfReader.Host/` is a runnable reference host for local testing only; it is not the package consumers should embed.
- `BookshelfReader.Tests/` covers DI wiring, option validation, pipeline components, and API behavior.

## Key Conventions

- Keep the consumer-facing NuGet surface in `BookshelfReader/`.
- Do not move product logic into `BookshelfReader.Host/`; it is only a reference host.
- Keep configuration bindable from `appsettings.json` and environment variables. Do not commit API keys or production secrets.
- Upload validation must stay strict: configured size limits, allowed JPEG/PNG content types, and signature checks protect the parse endpoint.
- API changes should preserve the documented `/api/books/lookup` and `/api/bookshelf/parse` contracts unless a breaking change is intentional and documented.
- The vision provider is pluggable. New providers implement `IVisionBookReader` in `Infrastructure/VisionLlm/`, add a `VisionProvider` enum value plus a `<Provider>VisionOptions` class (with API-key env-var fallback), and are wired in `BookshelfReaderServiceCollectionExtensions` via the `AddVisionBookReader` switch (bind/validate only the selected provider's options). Keep all provider calls behind `IVisionBookReader` so tests use fake `HttpMessageHandler`s and run offline. Claude is the default; do not change its behavior.
- Update `README.md` and `docs/IntegrationGuide.md` when changing package usage, endpoint shape, configuration keys, or deployment guidance.

## Configuration Notes

Important configuration sections include `Authentication:ApiKey`, `RateLimiting:Parse`, `Uploads`, `OpenLibrary`, `Enrichment`, and the vision sections. `Vision:Provider` selects the vision backend (`Claude` (default), `OpenAI`, or `Gemini`); only the selected provider's section is bound and validated. Each provider reads its key from its own section with an environment-variable fallback: `ClaudeVision:ApiKey`/`ANTHROPIC_API_KEY`, `OpenAIVision:ApiKey`/`OPENAI_API_KEY`, `GeminiVision:ApiKey`/`GEMINI_API_KEY`. The host fails fast at startup if the selected provider has no key.

Read `docs/IntegrationGuide.md` before making integration, deployment, or host-embedding changes.
