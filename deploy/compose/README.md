# Docker Compose deployment

`docker-compose.yaml` is the generated deployment view of the Aspire AppHost. The AppHost remains the source of truth for the resource graph; this file is the single Compose artifact that runs that graph as one application stack.

## Generate and run

From the repository root:

```bash
./scripts/generate-compose.sh
aspire deploy \
  --environment Development \
  --output-path ./aspire-output/compose \
  -- \
  --Demo:UseContainers=true \
  --Demo:UseLocalModel=true
```

`aspire deploy` prepares the generated Compose project, builds the API and web images, creates the persistent Qdrant and Ollama volumes, pulls `qwen2.5:3b` with `chat-model-loader`, and starts the stack. The generated environment file contains local secrets and ports and is intentionally not committed.

AI telemetry records metadata by default. For a deliberately local-only walkthrough that also records prompts, responses, tool arguments, and tool results, add `--Demo:CaptureTelemetryContent=true` to the Aspire publish/deploy command. Treat that mode as sensitive-data development instrumentation and never enable it for production workloads.

The reproducible evidence variant is:

```bash
CAPTURE_TELEMETRY_CONTENT=true \
EVIDENCE_OUTPUT_DIR="$PWD/docs/evidence/compose-content" \
./scripts/capture-compose-evidence.sh
```

To inspect the topology without starting it:

```bash
aspire publish \
  --output-path ./deploy/compose \
  -- \
  --Demo:UseContainers=true \
  --Demo:UseLocalModel=true
```

The generated services are:

```text
webfrontend -> apiservice -> cache (Redis)
                         -> qdrant
                         -> ollama
                         -> chat-model-loader (pulls qwen2.5:3b once)
compose-dashboard (OTLP traces)
```

The Compose dashboard is bound to `http://localhost:18888`. The web app receives that URL during the published Compose deployment and shows an **Open the Aspire dashboard** link on its homepage, next to the runtime status card. Use that link after the stack starts to review logs, traces, and metrics. The standalone Compose dashboard does not expose AppHost resource management; inspect container state with `docker compose ps` using the generated project and environment file.

If port `18888` is already in use, stop the conflicting process or Compose project before deployment. `capture-compose-evidence.sh` prints the generated project name, deployment directory, and exact `docker compose ... down` command so the inspected stack can be removed cleanly afterward.

The model loader is explicit because a logical Aspire model resource is not itself a long-running Compose service. The loader shares the Ollama data volume, waits for Ollama, and must complete successfully before the API starts.

The generated file uses `service_started` for the other dependencies, not Docker health checks. A started container may still be initializing. AppHost health waiting and Compose startup ordering therefore provide different guarantees: use the web health endpoint and a successful fresh request to confirm the deployed stack is ready. If the loader exits before Ollama is ready, rerun the deployment after inspecting its logs.

## Direct Compose use

The checked-in YAML contains image and secret placeholders. For a direct `docker compose` run, first use Aspire's preparation step so the application images and environment values exist:

```bash
aspire do prepare-compose \
  --environment Development \
  --output-path ./aspire-output/compose \
  -- \
  --Demo:UseContainers=true \
  --Demo:UseLocalModel=true
```

Then run the generated project with the environment file emitted in that output directory. Prefer `aspire deploy` for the walkthrough because it performs those preparation steps and prints the web endpoint in one command.
