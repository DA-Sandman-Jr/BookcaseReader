> **Auto-generated from `CLAUDE.md`** - edit the sibling `CLAUDE.md` instead. Changes made directly to this file will be overwritten on the next build by `Directory.Build.targets`.

# BookshelfReader - Agent Instructions

BookshelfReader provides .NET 8 dependency-injection helpers and optional API endpoint mappings for a bookshelf image parsing pipeline: upload validation, Claude vision book reading, genre classification, and Open Library lookup with in-pipeline metadata enrichment.

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

- `BookshelfReader/` is the single packable project. It contains shared domain contracts and models (`Core/`), concrete pipeline implementations and external integrations such as image preprocessing, the Claude vision book reader, genre classification, and Open Library lookup (`Infrastructure/`), service-registration helpers (`Extensions/`), and endpoint-mapping helpers and API surface (`Api/`). `dotnet pack` emits a single `BookshelfReader` NuGet package from this project.
- `BookshelfReader.Host/` is a runnable reference host for local testing only; it is not the package consumers should embed.
- `BookshelfReader.Tests/` covers DI wiring, option validation, pipeline components, and API behavior.

## Key Conventions

- Keep the consumer-facing NuGet surface in `BookshelfReader/`.
- Do not move product logic into `BookshelfReader.Host/`; it is only a reference host.
- Keep configuration bindable from `appsettings.json` and environment variables. Do not commit API keys or production secrets.
- Upload validation must stay strict: configured size limits, allowed JPEG/PNG content types, and signature checks protect the parse endpoint.
- API changes should preserve the documented `/api/books/lookup` and `/api/bookshelf/parse` contracts unless a breaking change is intentional and documented.
- Update `README.md` and `docs/IntegrationGuide.md` when changing package usage, endpoint shape, configuration keys, or deployment guidance.

## Configuration Notes

Important configuration sections include `Authentication:ApiKey`, `RateLimiting:Parse`, `Uploads`, `OpenLibrary`, `Enrichment`, and `ClaudeVision`. `ClaudeVision:ApiKey` (or the `ANTHROPIC_API_KEY` environment variable) is required - the host fails fast at startup without one.

Read `docs/IntegrationGuide.md` before making integration, deployment, or host-embedding changes.
