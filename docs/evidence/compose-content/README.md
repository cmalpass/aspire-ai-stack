# Content-enabled GenAI evidence

This directory contains a separate Compose run using synthetic prompts with development-only GenAI content capture enabled.

## What this proves

The run used the same generated seven-service Compose application as the default evidence path, but passed `--Demo:CaptureTelemetryContent=true`. The machine-readable manifest records that the switch was enabled and that the evidence prompt was synthetic.

The fresh trace was inspected in the Aspire dashboard at `/traces/detail/e486a5d23b70003f1b19b326a9839e88`. Its `chat qwen2.5:3b` GenAI details showed:

- the system instruction and synthetic user question;
- the assistant's `search_knowledge` tool call and query;
- the tool response containing the three grounded records;
- the final assistant answer;
- 666 input tokens, 128 output tokens, 794 total tokens, and one registered tool.

The trace still contains the same workflow, agent, retrieval, model, and cache boundaries as the metadata-only run. The difference is that the GenAI visualizer can now show the message sequence because the local development switch was explicitly enabled.

## Browser evidence

- [`compose-run.json`](compose-run.json) records the enabled capture flag, synthetic prompt, health result, dashboard link, sources, and fresh/cache results.
- [`compose-ui-fresh.png`](compose-ui-fresh.png) shows the homepage reporting **Content capture: Enabled for development**, alongside the real Ollama response and retrieved sources.
- [`compose-ui-cache-hit.png`](compose-ui-cache-hit.png) shows the repeated response served from Redis.
- [`aspire-dashboard-structured-logs.jpg`](aspire-dashboard-structured-logs.jpg) shows the dashboard's structured logs linking the web frontend and API activity to trace `e486a5d23b70003f1b19b326a9839e88`.
- [`aspire-dashboard-genai-metadata-only.jpg`](aspire-dashboard-genai-metadata-only.jpg) shows the default metadata-only GenAI panel: duration, token totals, and tool count are present, while message content is explicitly absent.
- [`aspire-dashboard-genai-details.jpg`](aspire-dashboard-genai-details.jpg) shows trace `e486a5d23b70003f1b19b326a9839e88` in the GenAI details panel with the synthetic prompt, grounded tool response, final output, and token totals.
- [`aspire-dashboard-genai-token-metrics.jpg`](aspire-dashboard-genai-token-metrics.jpg) shows the `apiservice` resource's `gen_ai.client.token.usage` Metrics view, with Ollama and `qwen2.5:3b` among the available tag values. Input and output samples are combined unless a token-type filter is selected; the percentiles are histogram aggregates, not per-request token totals.
- [`compose-trace.zip`](compose-trace.zip) contains the Playwright browser trace for the two UI interactions.

The dashboard screenshots were captured from the live local dashboard while the content-enabled run was available. The metadata-only comparison was captured from trace `aa4019b8dea31e4d2118813a26263344`; it records the absence of message content. The metrics screenshot includes an additional synthetic request (trace `b5c0e73813bf08190b31ed86d2264581`) after the automated run, so it is supplementary manual evidence rather than part of `compose-run.json`. Raw content-bearing dashboard trace exports are not included. The local dashboard can still be opened at `http://localhost:18888/traces` while the evidence project is running.

## Reproduce

```bash
CAPTURE_TELEMETRY_CONTENT=true \
EVIDENCE_OUTPUT_DIR="$PWD/docs/evidence/compose-content" \
./scripts/capture-compose-evidence.sh
```

Use synthetic questions only. Content capture is for local development diagnosis and should not be enabled for production workloads.
