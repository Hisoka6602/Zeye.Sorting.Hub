#!/usr/bin/env bash
# 中文说明：停止并卸载 Hub systemd 服务，保留发布文件和业务数据。
set -euo pipefail
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
exec bash "$script_dir/service.sh" uninstall "$@"
