#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"
deployment_dir="$(mktemp -d /tmp/aspire-compose-evidence.XXXXXX)"
log_path="${repo_root}/TestResults/compose-evidence/deploy.log"
evidence_dir="${repo_root}/docs/evidence/compose"

cd "${repo_root}"
mkdir -p "${repo_root}/TestResults/compose-evidence" "${evidence_dir}"

dotnet build AspireAiStack.sln --configuration Release
pwsh AspireAiStack.Tests/bin/Release/net10.0/playwright.ps1 install chromium

aspire deploy \
  --environment ComposeEvidence \
  --output-path "${deployment_dir}" \
  -- \
  --Demo:UseContainers=true \
  --Demo:UseLocalModel=true 2>&1 | tee "${log_path}"

web_url="$(rg -o 'Successfully deployed webfrontend to http://localhost:[0-9]+' "${log_path}" | tail -n 1 | sed 's/.* to //')"
if [[ -z "${web_url}" ]]; then
  echo "Could not find the deployed webfrontend URL in ${log_path}." >&2
  exit 1
fi

web_container_id="$(docker ps -q --filter label=com.docker.compose.service=webfrontend | head -n 1)"
if [[ -z "${web_container_id}" ]]; then
  echo "Could not find the running webfrontend Compose container." >&2
  exit 1
fi

compose_project="$(docker inspect --format '{{index .Config.Labels "com.docker.compose.project"}}' "${web_container_id}")"
docker ps -a \
  --filter "label=com.docker.compose.project=${compose_project}" \
  --format '{{json .}}' | jq -s '.' > "${evidence_dir}/compose-containers.json"

COMPOSE_BASE_URL="${web_url}" \
EVIDENCE_OUTPUT_DIR="${evidence_dir}" \
dotnet test AspireAiStack.Tests/AspireAiStack.Tests.csproj \
  --configuration Release \
  --no-build \
  --filter "FullyQualifiedName~ComposeBrowserEvidenceTests" \
  --logger "console;verbosity=normal" \
  --logger "trx;LogFileName=compose-evidence.trx" \
  --results-directory "${repo_root}/TestResults/compose-evidence"

echo "Compose evidence captured under ${evidence_dir}"
echo "The Compose project ${compose_project} is still running at ${web_url} for inspection."
