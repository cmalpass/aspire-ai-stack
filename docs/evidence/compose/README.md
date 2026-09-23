# Docker Compose evidence

This directory contains a real end-to-end run of the generated Docker Compose application. The run used .NET 10.0.1, Aspire 13.5.3, Docker Compose, Redis 8.6.7, Qdrant v1.18.0, Ollama 0.32.15, and the tool-capable `qwen2.5:3b` model.

## What this proves

The evidence follows one browser request through the complete stack:

1. The browser loads `webfrontend` and reads the status card from `apiservice`. The card reports `ollama`, `Qdrant`, and `Redis`.
2. The first prompt reaches the API. Qdrant returns three grounded records, Ollama generates the answer, and Redis stores the response.
3. The same prompt is submitted again. The API returns the same answer with `cacheHit: true`, proving the repeat path is served from Redis.
4. The web container's `/health` endpoint returns HTTP 200, proving the public entry point is ready after the Compose project starts.
5. The homepage exposes a verified link to the same Compose run's Aspire dashboard at `http://localhost:18888`.

The point is to make each claim inspectable rather than relying on a single screenshot:

| Artifact | Claim it supports |
| --- | --- |
| [`compose-run.json`](compose-run.json) | Health status, verified dashboard link, fresh/cache flags, source count, and identical answers |
| [`compose-containers.json`](compose-containers.json) | The running Compose project and its service/image identities, including the completed model loader |
| [`compose-ui-fresh.png`](compose-ui-fresh.png) | Live Ollama response, Qdrant sources, Redis status, and `FRESH RESPONSE` |
| [`compose-ui-cache-hit.png`](compose-ui-cache-hit.png) | The repeated request and visible `CACHE HIT` state |
| [`compose-trace.zip`](compose-trace.zip) | Playwright DOM snapshots, screenshots, and browser activity |

The running dashboard provides the telemetry walkthrough. Open `http://localhost:18888/traces`, select the fresh browser request, and inspect its trace detail: the verified trace shows `invoke_workflow grounded-rag-answer`, `invoke_agent grounded-answer-agent`, `chat qwen2.5:3b`, `execute_tool search_knowledge`, and `retrieval knowledge-store`. Opening the GenAI details for the chat shows token counts and one registered tool, while message content remains absent by default. The workflow and agent spans expose prompt/corpus versions, request ID, cache outcome, and source count. To inspect aggregate usage, open `http://localhost:18888/metrics`, select `apiservice`, then choose the `AspireAiStack.AI` meter and `gen_ai.client.token.usage` instrument. The API also emits app-owned outcome metrics through `AspireAiStack.ApiService`; the exact metric list can vary with the Aspire dashboard preview build.

For the visual privacy comparison and the opt-in conversation view, see the separate [`compose-content` evidence bundle](../compose-content/README.md). It includes screenshots of the metadata-only GenAI panel, the content-enabled GenAI panel, and the token-usage metrics table; it uses synthetic prompts and must remain a development-only exercise.

## How it maps to the article

The article's `AddRedis`, `AddQdrant`, `AddOllama`, project references, and `WaitFor` declarations describe the application graph once. `Aspire.Hosting.Docker` adds a Compose deployment target; `aspire publish` renders the graph to `docker-compose.yaml`, and `aspire deploy` builds the application images, fills the generated environment values, and runs that file.

The Compose file therefore is not a second hand-maintained architecture. It is the deployment-shaped view of the same AppHost model. The explicit `chat-model-loader` service is the one extra detail worth calling out: it pulls `qwen2.5:3b` into the shared Ollama volume and must finish successfully before the API starts.

## Reproduce

```bash
./scripts/capture-compose-evidence.sh
```

The script deliberately leaves the Compose project running so the web endpoint and the fixed dashboard URL (`http://localhost:18888`) can be opened for inspection. The generated environment file contains local secrets and remains outside source control.
