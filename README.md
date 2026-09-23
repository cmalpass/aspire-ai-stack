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

The eight tests cover deterministic embeddings, retrieval ordering, cache-key normalization, API validation and cache behavior, the Blazor status component, and an AppHost-managed API-to-web startup flow.

## Production boundary

The AppHost is an application model, not a production architecture decision. Before deployment, decide how identity, persistence, backups, private networking, scaling, model hosting, content safety, and observability retention should work. A typical Azure deployment replaces local containers with managed services and runs `aspire deploy` only after reviewing the generated plan.

## License

MIT. See [LICENSE](LICENSE).
