# BookshelfReader Integration Guide

This guide explains how to consume the BookshelfReader service from another web application while keeping each codebase, deployment, and secret store independent. It consolidates the runtime surface, configuration knobs, and collaboration practices your host team will need when proxying or embedding the service.

## 1. Runtime surface

### Supported commands

| Scenario | Command |
| --- | --- |
| Restore dependencies | `dotnet restore`
| Local development server | `dotnet run --project BookshelfReader.Api`
| Test suite | `dotnet test`
| Production build artifact | `dotnet publish BookshelfReader.Api -c Release`

Running `dotnet run --project BookshelfReader.Api` starts Kestrel on the default HTTPS port (`https://localhost:5001`) and HTTP port (`http://localhost:5000`). Swagger UI is automatically exposed in Development.

### HTTP endpoints

All routes are rooted at `/api` and described by the generated OpenAPI document at `/swagger/v1/swagger.json`.

| Method | Route | Description | Auth | Success |
| --- | --- | --- | --- | --- |
| `GET` | `/api/books/lookup?name=<title or author>` | Searches Open Library for matching metadata. | None | `200 OK` with `IEnumerable<BookMetadata>` JSON payload.
| `POST` | `/api/bookshelf/parse` | Accepts `multipart/form-data` with an `image` file containing a bookshelf photo and returns extracted spine candidates. | Optional API key (configurable) | `200 OK` with `ParseResult` JSON payload.

`ParseResult` provides:

```json
{
  "imageId": "<guid>",
  "books": [
    {
      "boundingBox": { "x": 0, "y": 0, "width": 0, "height": 0 },
      "title": "…",
      "author": "…",
      "genres": ["…"],
      "confidence": 0.82,
      "rawText": "…",
      "notes": ["…"]
    }
  ],
  "diagnostics": {
    "segmentCount": 3,
    "elapsedMs": 742,
    "notes": ["deskew applied"]
  }
}
```

Requests that violate validation (missing file, unsupported MIME type, OCR failure, etc.) return `400` responses with RFC 7807 bodies. When `RequireApiKey` is enabled the parse endpoint also returns `401` for missing or invalid keys.

## 2. Configuration & secrets

Configuration lives in `BookshelfReader.Api/appsettings.json` with environment-specific overrides in `appsettings.<Environment>.json`. Every option is also bindable via environment variables, which is the preferred mechanism for production secrets.

| Setting | Description | Environment variable example |
| --- | --- | --- |
| `Authentication:ApiKey:HeaderName` | HTTP header that carries the credential. | `Authentication__ApiKey__HeaderName=X-API-Key`
| `Authentication:ApiKey:RequireApiKey` | Enables API-key enforcement on `/api/bookshelf/parse`. | `Authentication__ApiKey__RequireApiKey=true`
| `Authentication:ApiKey:ValidKeys` | List of accepted API keys. Omit in source control. | `Authentication__ApiKey__ValidKeys__0=<value>`
| `OpenLibrary:BaseUrl` | Override Open Library endpoint (for mocks or future changes). | `OpenLibrary__BaseUrl=https://...`
| `Uploads:MaxBytes` | Maximum upload size in bytes. | `Uploads__MaxBytes=10485760`
| `Uploads:AllowedContentTypes` | Canonical MIME types accepted from the form upload. | `Uploads__AllowedContentTypes__0=image/jpeg`
| `Ocr:Tesseract:*` | File system path, languages, and concurrency for OCR. | `Ocr__Tesseract__DataPath=/app/tessdata`
| `Segmentation:*` / `Parsing:*` | Heuristics controlling contour filtering and text parsing. | `Segmentation__MaxSegments=64`

Production deployments should inject secrets through your platform’s secret store (Azure Key Vault, AWS Secrets Manager, GitHub Actions secrets → environment variables). Only ship sanitized sample values in source control. The development profile (`appsettings.Development.json`) demonstrates setting a disposable key (`local-dev-key`).

### Sample `.env` (development)

```
ASPNETCORE_ENVIRONMENT=Development
Authentication__ApiKey__RequireApiKey=true
Authentication__ApiKey__ValidKeys__0=local-dev-key
Ocr__Tesseract__DataPath=./tessdata
```

Load it with `dotnet user-secrets`, `direnv`, or your preferred tooling.

## 3. Integration contract

1. **Authentication** – When `RequireApiKey` is `true`, callers must include `X-API-Key: <value>` (or your configured header) on every `POST /api/bookshelf/parse` request. The lookup endpoint is anonymous by default.
2. **Payloads** – The upload endpoint requires `multipart/form-data` with a single `image` file field. JPEG and PNG inputs are accepted out of the box. The service streams files directly from the request and rejects payloads larger than the configured limit or whose signatures do not match the MIME type.
3. **Response shape** – Use the `ParseResult` schema above. Bounding boxes are relative to the uploaded image, enabling downstream UI overlays.
4. **Errors** – Validation problems use the standard ASP.NET Core RFC 7807 schema with `errors` dictionary. Treat 4xx codes as actionable client fixes and 5xx codes as transient failures worth retrying.
5. **OpenAPI** – Consume `/swagger/v1/swagger.json` to generate typed clients (NSwag, AutoRest, openapi-typescript). Regenerate clients whenever the service bumps its version.

### UI embedding

BookshelfReader does not ship standalone UI assets. The recommended integration pattern is either:

* **Reverse proxy** – Surface the API under your main site (e.g., `/api/bookshelf/*`) via a reverse proxy or API gateway.
* **Iframe / widget** – Host your own UI and call the service from the browser via HTTPS. If you need cross-origin access, configure CORS in your host proxy; the service itself is locked down with strict security headers and same-origin policies.

## 4. Deployment boundaries

* Package and deploy the service independently (App Service, container app, Kubernetes workload, etc.).
* Do not share infrastructure resources (storage, queues) with the host site unless absolutely necessary; BookshelfReader is stateless and depends only on outbound HTTP to Open Library and the local file system for OCR assets.
* Expose the service via a dedicated subdomain (e.g., `https://bookshelf-reader.yourdomain.com`) or a reverse-proxy path segment. Ensure TLS termination happens at the edge the same way as your primary site.
* If you front the service with an ingress controller or API gateway, forward the `X-API-Key` header without modification and enforce rate limits upstream.

## 5. Local co-development workflow

1. Clone your main site repo and this repo side by side.
2. Start BookshelfReader: `dotnet run --project BookshelfReader.Api` (or rely on the defaults).
3. Start your host site on a different port (e.g., `https://localhost:7000`).
4. Configure the host to call BookshelfReader by setting an environment variable such as `BOOKSHELF_READER_BASE_URL=https://localhost:5001`.
5. For browser-based integration, either:
   * Proxy `/api/bookshelf/*` through your host dev server to avoid CORS, or
   * Enable CORS on your host proxy to allow calls from `https://localhost:7000` to `https://localhost:5001`. BookshelfReader itself does not register CORS; manage it at the proxy.
6. Use sample curl commands (`curl -H "X-API-Key: local-dev-key" -F "image=@sample.jpg" https://localhost:5001/api/bookshelf/parse`) to verify connectivity.

Troubleshooting tips:

* **401 Unauthorized** – Confirm `RequireApiKey` is enabled only when you supply the header. Rotate keys via configuration without redeploying.
* **400 Bad Request** – Ensure the file field name is exactly `image` and the MIME type is allowed.
* **Large file rejection** – Increase `Uploads:MaxBytes` temporarily during debugging.
* **OCR missing data** – Populate `Ocr:Tesseract:DataPath` with valid traineddata files.

## 6. Validation & CI expectations

The repository targets .NET 8. A healthy pipeline should execute:

```
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

Consider publishing a Docker image (`dotnet publish -c Release -o out && docker build`) or versioned release artifacts. Tag releases using semantic versioning (e.g., `v1.4.0`) so the host site can pin to a known build.

## 7. Release coordination

* Document API or response-shape changes in `CHANGELOG.md` or the README release notes section.
* Publish GitHub releases summarizing changes. Include upgrade notes for contract-affecting updates.
* For breaking changes, bump the major version and announce via your team’s communication channel (Slack, Teams, email).
* When introducing new optional fields, guard the behavior with feature flags so the host site can opt in safely.

Following this guide will let you operate BookshelfReader as an autonomous service while providing a clear contract for your main site to call into whenever you are ready to embed bookshelf parsing features.
