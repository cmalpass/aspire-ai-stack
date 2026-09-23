# Aspire AI Stack

A runnable .NET 10 and Aspire 13.5.3 companion for the article **Building Local AI Applications with .NET Aspire**.

The sample keeps the application boundaries visible:

- Aspire AppHost declares the resource graph, references, health checks, and startup ordering.
- ASP.NET Core owns retrieval, caching, model access, and telemetry.
- Blazor provides a small interactive client without receiving infrastructure credentials.
- Qdrant stores deterministic demo vectors.
- Redis caches repeated responses.
- Ollama is available as the optional local model host.

The default uses real Redis and Qdrant containers but a deterministic answer generator. This makes the first run useful without downloading a model or configuring a cloud credential.

## Prerequisites

- [.NET SDK 10.0.100 or later](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Aspire CLI 13.5.3](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling)
- Docker Desktop, Podman, or another [supported OCI container runtime](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling)

If the AppHost reports an untrusted development certificate, run `aspire certs trust` before starting it. The automated AppHost test uses HTTP and does not require a trusted certificate.

## Run the stack

```bash
dotnet restore AspireAiStack.sln
dotnet run --project AspireAiStack.AppHost
```

Open the dashboard URL printed in the terminal, then select the `webfrontend` endpoint. Ask the same question twice: the first response is generated and the second reports a cache hit.

The default resource graph is:

```text
webfrontend -> apiservice -> qdrant
                         -> cache (Redis)
                         -> ollama
```

## Enable a local model

Set `Demo:UseLocalModel` when starting the AppHost:

```bash
dotnet run --project AspireAiStack.AppHost -- --Demo:UseLocalModel=true
```

Aspire adds a `phi3:mini` model resource and waits for it before starting the API. The first run downloads the model and therefore takes longer. The API talks to Ollama through `IChatClient` from `Microsoft.Extensions.AI`; the browser never receives the model endpoint.

## Run the whole stack from one Compose file

The AppHost is the source of truth, and Aspire can render that resource graph as one Docker Compose application. Generate the checked-in deployment view with:

```bash
./scripts/generate-compose.sh
```

For a complete live run that builds the API and web images, starts Redis, Qdrant, Ollama, the model loader, and the Aspire dashboard, then captures evidence through the browser:

```bash
./scripts/capture-compose-evidence.sh
```

The script leaves the Compose project running for inspection. It writes the health result, fresh response, cache-hit response, container inventory, screenshots, and Playwright trace to `docs/evidence/compose/`. See [`deploy/compose/README.md`](deploy/compose/README.md) for how AppHost declarations become Compose services and why the model loader is a separate one-shot service.

## Run without containers

The credential-free mode is useful for tests and environments without an OCI runtime:

```bash
dotnet run --project AspireAiStack.AppHost -- --Demo:UseContainers=false
```

That mode substitutes deterministic in-memory vector search and response caching while preserving the API and UI flow.

## Verify the sample

```bash
dotnet build AspireAiStack.sln --configuration Release
dotnet test AspireAiStack.sln --configuration Release --no-build
```

The test suite covers deterministic embeddings, retrieval ordering, cache-key normalization, Ollama connection-string parsing, API validation and cache behavior, the Blazor status component, an AppHost-managed API-to-web startup flow, and the Chromium smoke path.

### Browser smoke test

The ordinary test suite also drives the credential-free flow in headless Chromium and records a screenshot plus a Playwright trace. Install the matching browser once after building:

```bash
dotnet build AspireAiStack.sln --configuration Release
pwsh AspireAiStack.Tests/bin/Release/net10.0/playwright.ps1 install chromium
dotnet test AspireAiStack.sln --configuration Release --no-build
```

CI uploads the browser screenshot, trace, and TRX results as a `browser-smoke-evidence` artifact.

### Capture real-model evidence

The live evidence test is intentionally opt-in because it starts Redis, Qdrant, and Ollama containers and downloads `phi3:mini` on the first run. It exercises a fresh model response and a Redis cache hit through both HTTP and the Blazor UI:

```bash
./scripts/capture-live-model-evidence.sh
```

The script writes curated JSON, an HTTP transcript, and fresh/cache-hit screenshots to `docs/evidence/`. The live test is skipped during ordinary `dotnet test` and CI runs; set `RUN_LIVE_MODEL_E2E=true` only when Docker has enough time and disk space for the model.

## Production boundary

The AppHost is an application model, not a production architecture decision. Before deployment, decide how identity, persistence, backups, private networking, scaling, model hosting, content safety, and observability retention should work. A typical Azure deployment replaces local containers with managed services and runs `aspire deploy` only after reviewing the generated plan.

## License

MIT. See [LICENSE](LICENSE).
