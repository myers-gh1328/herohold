#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$repo_root/.tmp/dotnet-home}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
export NUGET_PACKAGES="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
cd "$repo_root"
exec dotnet run --project dotnet/Herohold.Web/Herohold.Web.csproj --no-launch-profile -- --urls "http://127.0.0.1:${HEROHOLD_PORT:-8510}"
