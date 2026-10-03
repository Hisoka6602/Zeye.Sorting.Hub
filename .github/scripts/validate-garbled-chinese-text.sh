#!/usr/bin/env bash

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PROJECT_FILE="${REPO_ROOT}/Zeye.Sorting.Hub.Host/Zeye.Sorting.Hub.Host.csproj"

# 中文说明：使用连字符参数，避免 Windows Git Bash 将斜杠参数转换成文件路径。
dotnet msbuild "${PROJECT_FILE}" -t:FailBuildWhenChineseTextLooksGarbled -nologo
