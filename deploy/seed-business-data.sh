#!/usr/bin/env sh
# 中文说明：Linux本机Docker使用与Windows完全相同的领域模型和模拟批次。
set -eu
script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repository_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
count=${1:-10000}
days=${2:-30}
mode=${3:---write}
public_base_url=${4:-http://127.0.0.1:4187}
case "$mode" in --write|--verify-only|--preview) ;; *) echo '第三个参数为--write、--verify-only或--preview' >&2; exit 1 ;; esac
publish_directory=$(mktemp -d -t zeye-business-simulator-XXXXXXXX)
dotnet publish "$repository_root/tools/BusinessDataSimulator/BusinessDataSimulator.csproj" -c Release --nologo -v quiet -clp:ErrorsOnly -o "$publish_directory"
if [ "$mode" = '--preview' ]; then
    dotnet "$publish_directory/BusinessDataSimulator.dll" --count "$count" --days "$days" --public-base-url "$public_base_url"
else
    container_id=$(docker compose --env-file "$script_dir/.env" -f "$script_dir/compose.yaml" ps -q host)
    [ -n "$container_id" ] || { echo '未发现本项目运行中的Host容器。' >&2; exit 1; }
    docker cp "$publish_directory/." "$container_id:/tmp/zeye-business-simulator"
    docker compose --env-file "$script_dir/.env" -f "$script_dir/compose.yaml" exec -T host dotnet /tmp/zeye-business-simulator/BusinessDataSimulator.dll --local-docker "$mode" --count "$count" --days "$days" --public-base-url "$public_base_url"
fi
printf '工具发布目录：%s\n' "$publish_directory"
