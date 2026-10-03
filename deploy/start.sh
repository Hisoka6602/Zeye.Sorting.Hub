#!/usr/bin/env bash
# 中文说明：Linux 部署入口，等待前端和数据库接口就绪后再报告成功。
set -euo pipefail

skip_build=false
open_browser=true
startup_timeout=180
while (($#)); do
  case "$1" in
    --skip-build) skip_build=true; shift ;;
    --no-open-browser) open_browser=false; shift ;;
    --startup-timeout)
      [[ $# -ge 2 && "$2" =~ ^[0-9]{1,4}$ ]] || { echo '等待时间需要是 10～3600 秒的整数。' >&2; exit 2; }
      startup_timeout=$((10#$2))
      ((startup_timeout >= 10 && startup_timeout <= 3600)) || { echo '等待时间需要是 10～3600 秒的整数。' >&2; exit 2; }
      shift 2 ;;
    --help)
      echo '用法：bash deploy/start.sh [--skip-build] [--no-open-browser] [--startup-timeout 180]'
      exit 0 ;;
    *) echo "未知参数：$1" >&2; exit 2 ;;
  esac
done

for dependency in docker curl; do
  command -v "$dependency" >/dev/null || { echo "缺少命令：$dependency" >&2; exit 1; }
done
script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
[[ -f "$script_directory/.env" ]] || { echo '请先复制 deploy/.env.example 为 deploy/.env，并设置数据库密码。' >&2; exit 1; }
compose=(docker compose --env-file "$script_directory/.env" --file "$script_directory/compose.yaml")
build_argument=--build
if "$skip_build"; then build_argument=--no-build; fi
"${compose[@]}" up --detach --wait --wait-timeout "$startup_timeout" "$build_argument"
"${compose[@]}" exec -T mysql sh -c 'MYSQL_PWD="${MYSQL_ROOT_PASSWORD}" mysql -uroot' < "$script_directory/initialize-restore.sql"
binding="$("${compose[@]}" port web 8080)"
[[ "$binding" =~ ^127\.0\.0\.1:([0-9]{1,5})$ ]] || { echo "前端端口绑定不符合本机部署配置：$binding" >&2; exit 1; }
web_port=$((10#${BASH_REMATCH[1]}))
((web_port >= 1 && web_port <= 65535)) || { echo '前端端口无效。' >&2; exit 1; }
frontend_url="http://127.0.0.1:$web_port/data-overview"
health_url="http://127.0.0.1:$web_port/health/ready"
deadline=$((SECONDS + startup_timeout))
ready=false
while ((SECONDS < deadline)); do
  if curl --fail --silent --max-time 5 --output /dev/null "$frontend_url" &&
     health="$(curl --fail --silent --max-time 5 "$health_url")" &&
     [[ "$health" =~ \"status\"[[:space:]]*:[[:space:]]*\"([^\"]+)\" ]] &&
     [[ "${BASH_REMATCH[1]}" == Healthy ]]; then
    ready=true
    break
  fi
  sleep 2
done
if ! "$ready"; then
  echo "前端或 API 未在 $startup_timeout 秒内就绪，请检查 Compose 服务日志。" >&2
  exit 1
fi
echo "部署完成，前端地址：$frontend_url"
if "$open_browser" && [[ -n "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ]] && command -v xdg-open >/dev/null; then
  xdg-open "$frontend_url" >/dev/null 2>&1 || echo "无法自动打开浏览器，请手动访问：$frontend_url" >&2
fi
