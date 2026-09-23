# Live model evidence

This directory contains the output of a real end-to-end run captured on 2026-09-23. The Aspire AppHost started the API and Blazor UI, reused its persistent Redis, Qdrant, and Ollama resources, and waited for the `phi3:mini` model before the test sent traffic.

## Verified path

The test exercises two independent entry points:

1. A direct HTTP request reaches `/api/chat`, retrieves three records from Qdrant, generates an answer with Phi-3 through Ollama, and stores the result in Redis.
2. Repeating the same HTTP request returns the identical answer with `cacheHit: true`.
3. Chromium loads the Blazor UI, submits a different prompt, and records the fresh model response with three retrieved sources.
4. Chromium repeats the prompt and records the identical response with the visible `CACHE HIT` state.

The machine-readable assertions and complete payloads are in [`live-model-run.json`](live-model-run.json). The raw request/response pair is in [`http-transcript.txt`](http-transcript.txt).

## Browser evidence

- [`live-model-ui-fresh.png`](live-model-ui-fresh.png) shows the real model response with Ollama, Qdrant, and Redis active.
- [`live-model-ui-cache-hit.png`](live-model-ui-cache-hit.png) shows the repeated request served from Redis.
- `live-model-trace.zip` is the Playwright trace containing DOM snapshots, screenshots, and browser activity for both interactions.

## Runtime identities

| Resource | Version | Immutable identity |
| --- | --- | --- |
| Ollama | `0.32.15` | `sha256:57d60e686821ea81a7748a3ec8141308c8b8f95b27105713954abf7a6529e700` |
| Phi-3 Mini | `phi3:mini` | Ollama model `4f2222927938`; model blob `sha256:633fc5be925f9a484b61d6f9b9a78021eeb462100bd557309f01ba84cac26adf` |
| Qdrant | `v1.18.0` | `sha256:b3063c673f3973877c038eeecc392bad5011f072ee7892b56c9a8e204a3bdea9` |
| Redis | `8.6` (server `8.6.7`) | `sha256:6d0978c640bd9b2ee095a08603dfbf855452f168584a0d7ef1e84f43a0859576` |

The run used .NET 10.0.1 on macOS 26.5.2. The model ran on CPU, so generation time will vary by host.

## Reproduce

Install Docker, .NET 10, and PowerShell, then run:

```bash
./scripts/capture-live-model-evidence.sh
```

The script builds the solution, installs the pinned Playwright Chromium browser, opts into the live-model test, writes a TRX result under `TestResults/live-model`, and refreshes this directory's generated files. The live test is intentionally excluded from the ordinary test run unless `RUN_LIVE_MODEL_E2E=true` is set.
