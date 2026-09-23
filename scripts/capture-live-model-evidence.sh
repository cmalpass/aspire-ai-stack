#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"

cd "${repo_root}"

dotnet build AspireAiStack.sln --configuration Release
pwsh AspireAiStack.Tests/bin/Release/net10.0/playwright.ps1 install chromium

RUN_LIVE_MODEL_E2E=true \
EVIDENCE_OUTPUT_DIR="${repo_root}/docs/evidence" \
dotnet test AspireAiStack.Tests/AspireAiStack.Tests.csproj \
  --configuration Release \
  --no-build \
  --filter "FullyQualifiedName~LiveModelEndToEndTests" \
  --logger "console;verbosity=normal" \
  --logger "trx;LogFileName=live-model.trx" \
  --results-directory "${repo_root}/TestResults/live-model"

echo "Evidence captured under ${repo_root}/docs/evidence"
