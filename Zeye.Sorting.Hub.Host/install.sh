#!/usr/bin/env bash
# 中文说明：从完整发布目录安装并启动 Hub systemd 服务。
set -euo pipefail
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
exec bash "$script_dir/service.sh" install "$@"
