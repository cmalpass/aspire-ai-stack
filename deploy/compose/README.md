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

`aspire deploy` prepares the generated Compose project, builds the API and web images, creates the persistent Qdrant and Ollama volumes, pulls `phi3:mini` with `chat-model-loader`, and starts the stack. The generated environment file contains local secrets and ports and is intentionally not committed.

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
                         -> chat-model-loader (pulls phi3:mini once)
compose-dashboard (OTLP traces)
```

The Compose dashboard is bound to `http://localhost:18888`. The web app receives that URL during the published Compose deployment and shows an **Open the Aspire dashboard** link on its homepage, next to the runtime status card. Use that link after the stack starts to review service health, logs, traces, and metrics for the same run.

If port `18888` is already in use, stop the conflicting process or Compose project before deployment. `capture-compose-evidence.sh` prints the generated project name, deployment directory, and exact `docker compose ... down` command so the inspected stack can be removed cleanly afterward.

The model loader is explicit because a logical Aspire model resource is not itself a long-running Compose service. The loader shares the Ollama data volume, waits for Ollama, and must complete successfully before the API starts.

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
