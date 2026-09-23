#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

for required_command in dotnet aspire; do
  if ! command -v "${required_command}" >/dev/null 2>&1; then
    echo "Required command '${required_command}' was not found on PATH." >&2
    exit 1
  fi
done

cd "${repo_root}"
mkdir -p deploy/compose

aspire publish \
  --output-path deploy/compose \
  -- \
  --Demo:UseContainers=true \
  --Demo:UseLocalModel=true

echo "Generated ${repo_root}/deploy/compose/docker-compose.yaml"
