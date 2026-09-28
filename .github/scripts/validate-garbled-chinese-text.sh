#!/usr/bin/env bash

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PROJECT_FILE="${REPO_ROOT}/Zeye.Sorting.Hub.Host/Zeye.Sorting.Hub.Host.csproj"

dotnet msbuild "${PROJECT_FILE}" /t:FailBuildWhenChineseTextLooksGarbled /nologo
