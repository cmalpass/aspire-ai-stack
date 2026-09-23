# Aspire AI Stack

A runnable .NET 10 and Aspire 13.5.3 companion for the article [**Building Local AI Applications with .NET Aspire**](https://chrismalpass.com/posts/building-ai-applications-dotnet-aspire/).

The sample keeps the application boundaries visible:

- Aspire AppHost declares the resource graph, references, health checks, and startup ordering.
- ASP.NET Core owns retrieval, caching, model access, and telemetry.
- Blazor provides a small interactive client without receiving infrastructure credentials.
- Qdrant stores deterministic demo vectors.
- Redis caches repeated responses.
- Ollama is available as the optional local model host.

The default uses real Redis and Qdrant containers but a deterministic answer generator. This makes the first run useful without downloading a model or configuring a cloud credential.

## Prerequisites

- [.NET SDK 10.0.101 or a compatible 10.0 patch](https://dotnet.microsoft.com/download/dotnet/10.0) (`global.json` pins the reviewed feature band)
- [Aspire CLI 13.5.3](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling)
- Docker Desktop, Podman, or another [supported OCI container runtime](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling)

If the AppHost reports an untrusted development certificate, run `aspire certs trust` before starting it. The automated AppHost test uses HTTP and does not require a trusted certificate.

The evidence-capture scripts also check for PowerShell (`pwsh`), ripgrep (`rg`), and—on the Compose path—`jq`. These are not required for the ordinary first run.

## Choose a run path

| Goal | Command | What runs |
| --- | --- | --- |
| Normal development | `dotnet run --project AspireAiStack.AppHost` | Real Redis and Qdrant, deterministic generation |
| Fastest credential-free check | Add `-- --Demo:UseContainers=false` | In-memory retrieval and cache, deterministic generation |
| Real local generation | Add `-- --Demo:UseLocalModel=true` | Redis, Qdrant, Ollama, and `phi3:mini` |
| Full Compose proof | `./scripts/capture-compose-evidence.sh` | Generated seven-service Compose project plus browser evidence |

## Run the stack

```bash
dotnet restore AspireAiStack.sln
dotnet run --project AspireAiStack.AppHost
```

Open the dashboard URL printed in the terminal, then select the `webfrontend` endpoint. The homepage lists the seven architecture topics that are already seeded and supplies a suggested question for each one.

For the verified walkthrough:

1. Select **AppHost orchestration** or use the prefilled question.
2. Submit it and expect a **fresh response** with three retrieved context records.
3. Submit the same question again and expect the identical answer with **cache hit**.

The first Qdrant-backed request can take a little longer while the seven records are created or refreshed. The runtime card shows which AI, vector-store, and cache implementations are active.

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

The script leaves the Compose project running for inspection. It prints the web URL, the fixed dashboard URL (`http://localhost:18888`), the generated deployment directory, and an exact cleanup command. It writes the health result, fresh response, cache-hit response, container inventory, screenshots, and Playwright trace to `docs/evidence/compose/`. See [`deploy/compose/README.md`](deploy/compose/README.md) for how AppHost declarations become Compose services and why the model loader is a separate one-shot service.

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

The test suite covers deterministic embeddings, retrieval ordering, cache-key normalization, Ollama connection-string parsing, API validation and cache behavior, the Blazor status component, an AppHost-managed API-to-web startup flow, and the Chromium smoke path—including selection of a seeded question.

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

The script writes curated JSON, an HTTP transcript, and fresh/cache-hit screenshots to `docs/evidence/`. The live-model and deployed-Compose tests are skipped during ordinary `dotnet test` and CI runs; set `RUN_LIVE_MODEL_E2E=true` only when Docker has enough time and disk space for the model.

## Swap the model provider

The browser and `AiAssistantService` depend on the API's `IAnswerGenerator`, while the Ollama implementation receives `Microsoft.Extensions.AI.IChatClient`. To use Azure OpenAI or another provider, replace the `AddChatClient` registration in `AspireAiStack.ApiService/Program.cs`, keep credentials in server-side configuration, and leave the browser contract unchanged. Include the provider, model version, generation settings, and grounding-corpus version in any production cache key.

## Troubleshooting

- **The first local-model start is slow:** Ollama must download `phi3:mini`; the evidence tests allow up to 20 minutes for a cold run.
- **Port 18888 is already in use:** stop the conflicting process or Compose project before running the Compose evidence path.
- **The first Qdrant answer is slower:** the API creates or refreshes the seven seeded records on the first search.
- **The UI says the API is not ready:** use the homepage retry button or inspect `apiservice` in the Aspire dashboard.
- **A script reports a missing command:** install the named prerequisite and rerun it; the scripts fail before changing the deployment when a required tool is absent.

## Production boundary

The AppHost is an application model, not a production architecture decision. Before deployment, decide how identity, persistence, backups, private networking, scaling, model hosting, content safety, and observability retention should work. A typical Azure deployment replaces local containers with managed services and runs `aspire deploy` only after reviewing the generated plan.

## License

MIT. See [LICENSE](LICENSE).
