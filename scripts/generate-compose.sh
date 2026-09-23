#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

if ! command -v aspire >/dev/null 2>&1; then
  echo "Aspire CLI 13.5.3 or later is required. Install it from https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling." >&2
  exit 1
fi

cd "${repo_root}"
mkdir -p deploy/compose

aspire publish \
  --output-path deploy/compose \
  -- \
  --Demo:UseContainers=true \
  --Demo:UseLocalModel=true

echo "Generated ${repo_root}/deploy/compose/docker-compose.yaml"
