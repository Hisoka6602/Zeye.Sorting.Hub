#!/usr/bin/env bash
# 中文说明：Linux systemd 安装、更新及停止卸载的共用实现。
set -euo pipefail
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
action="${1:-}"
[[ $# -gt 0 ]] && shift
service_name="${ZEYE_SERVICE_NAME:-Zeye.Sorting.Hub.Host}"
service_user="${ZEYE_SERVICE_USER:-zeye-hub}"
service_group="${ZEYE_SERVICE_GROUP:-$service_user}"
unit_name="$service_name.service"
unit_path="/etc/systemd/system/$unit_name"
env_file="${ZEYE_SERVICE_ENV_FILE:-/etc/default/$service_name}"
binary="$script_dir/Zeye.Sorting.Hub.Host"
timeout_seconds="${ZEYE_SERVICE_TIMEOUT_SECONDS:-180}"
dry_run=false
[[ "${ZEYE_SERVICE_DRY_RUN:-}" =~ ^(1|[Tt][Rr][Uu][Ee]|[Yy][Ee][Ss]|[Oo][Nn])$ ]] && dry_run=true

# 中文说明：输出错误原因并中止，避免部分失败仍显示安装或卸载成功。
fail() { printf '错误：%s\n' "$*" >&2; exit 1; }
trap 'printf "服务 %s 失败（行 %s）；请检查 systemctl status 和发布目录 logs。\n" "$action" "$LINENO" >&2' ERR

# 中文说明：按 systemd 属性解析方式处理路径，工作目录和环境文件不接受外层引号。
unit_quote() {
    local value="$1"
    value="${value//%/%%}"
    if [[ "${2:-}" != executable ]]; then printf '%s' "$value"; return; fi
    value="${value//\\/\\\\}"
    value="${value//\"/\\\"}"
    # ExecStart 的可执行文件路径不展开环境变量，美元字符需要原样保留。
    printf '"%s"' "$value"
}

# 中文说明：仅接管当前发布目录创建的常规 unit 文件，名称冲突不会覆盖其他服务。
assert_ownership() {
    local fragment
    fragment="$(systemctl show "$unit_name" --property=FragmentPath --value)"
    if [[ -e "$unit_path" || -L "$unit_path" ]]; then
        [[ ! -L "$unit_path" && -f "$unit_path" ]] || fail '服务单元是符号链接或特殊文件，不能覆盖或删除。'
        grep -Fxq -- "# ZeyeHubInstallDirectory=$script_dir" "$unit_path" || fail '同名服务属于其他发布目录，请从原目录卸载或设置不同服务名。'
        [[ -z "$fragment" || "$fragment" == "$unit_path" ]] || fail '同名服务由其他系统目录管理，不能接管。'
    elif [[ -n "$fragment" ]]; then
        fail '已存在由其他目录管理的同名服务，不能接管。'
    fi
}

# 中文说明：配置损坏且没有运行进程的自有服务也能卸载，真实停止失败仍然阻断后续操作。
stop_owned_service() {
    local load_state active_state main_pid
    load_state="$(systemctl show "$unit_name" --property=LoadState --value)"
    if [[ "$load_state" == bad-setting || "$load_state" == not-found ]]; then
        active_state="$(systemctl show "$unit_name" --property=ActiveState --value)"
        main_pid="$(systemctl show "$unit_name" --property=MainPID --value)"
        [[ "$active_state" == inactive && "$main_pid" == 0 ]] || fail '配置异常的服务仍有运行状态，不能删除注册文件。'
        return 0
    fi
    systemctl stop "$unit_name"
}

# 中文说明：启动后按需验证业务就绪探针；systemd 自身会等待宿主的 READY 通知。
wait_ready() {
    local deadline=$((SECONDS + timeout_seconds)) status
    [[ -n "${ZEYE_SERVICE_HEALTH_URL:-}" ]] || return 0
    while (( SECONDS < deadline )); do
        systemctl is-active --quiet "$unit_name" || fail '服务启动后已停止，请检查日志。'
        if status="$(curl --silent --max-time 5 --output /dev/null --write-out '%{http_code}' "$ZEYE_SERVICE_HEALTH_URL")" && [[ "$status" == 200 ]]; then return 0; fi
        sleep 1
    done
    fail '就绪探针等待超时，请核对监听地址和数据库配置。'
}

[[ "$action" == install || "$action" == uninstall ]] || fail '用法：install.sh / uninstall.sh [--dry-run]'
for argument in "$@"; do
    [[ "$argument" == --dry-run ]] || fail "未知参数：$argument"
    dry_run=true
done
[[ "$service_name" =~ ^[A-Za-z0-9][A-Za-z0-9_.-]{0,79}$ ]] || fail '服务名只允许字母、数字、点、下划线和连字符，长度为 1～80。'
[[ "$service_user" =~ ^[a-z_][a-z0-9_-]{0,31}$ && "$service_group" =~ ^[a-z_][a-z0-9_-]{0,31}$ ]] || fail '服务用户和组名必须是有效的 Linux 本地账号名。'
[[ "$timeout_seconds" =~ ^[1-9][0-9]{1,2}$ ]] && (( timeout_seconds >= 30 && timeout_seconds <= 900 )) || fail '服务超时必须为 30～900 的整数秒数。'
[[ "$script_dir" != / ]] || fail '发布包不能位于文件系统根目录。'
[[ "$script_dir" != *'"'* && "$script_dir" != *'\'* ]] || fail 'systemd 可执行文件路径不能包含引号或反斜线，请使用其他发布目录。'
[[ "$env_file" == /* && "$env_file" != *$'\n'* && "$env_file" != *$'\r'* && "$script_dir" != *$'\n'* && "$script_dir" != *$'\r'* ]] || fail '配置文件必须是绝对路径，发布路径和配置路径不能包含换行。'
[[ -z "${ZEYE_SERVICE_HEALTH_URL:-}" || "$ZEYE_SERVICE_HEALTH_URL" =~ ^https?://[^[:space:]]+$ ]] || fail 'ZEYE_SERVICE_HEALTH_URL 必须是 HTTP 或 HTTPS 的完整就绪地址。'
if [[ "$action" == install ]]; then
    for relative in Zeye.Sorting.Hub.Host wwwroot/index.html appsettings.json; do
        [[ -f "$script_dir/$relative" && ! -L "$script_dir/$relative" ]] || fail "缺少常规文件 $relative，请使用完整的 Linux 自包含发布包。"
    done
fi
if $dry_run; then
    printf '[预演] %s 服务：%s\n[预演] 发布目录：%s\n[预演] 环境配置：%s\n' "$action" "$unit_name" "$script_dir" "$env_file"
    printf '[预演] 安装使用专用账号并启用自动启动；卸载等待停止后移除 unit，保留全部配置与数据。\n'
    exit 0
fi
(( EUID == 0 )) || fail '请使用 sudo / root 运行安装或卸载脚本。'
for command in systemctl grep; do command -v "$command" >/dev/null || fail "缺少命令：$command"; done
if [[ -n "${ZEYE_SERVICE_HEALTH_URL:-}" ]]; then command -v curl >/dev/null || fail '配置就绪探针后需要 curl。'; fi
assert_ownership

if [[ "$action" == uninstall ]]; then
    if [[ ! -e "$unit_path" ]]; then
        printf '服务 %s 未安装；发布文件、配置和数据保留。\n' "$unit_name"
        exit 0
    fi
    # 停止失败时不继续删除注册文件，避免运行中的服务脱离管理。
    stop_owned_service
    systemctl disable "$unit_name"
    rm -- "$unit_path"
    systemctl daemon-reload
    systemctl reset-failed "$unit_name" 2>/dev/null || true
    printf '服务 %s 已停止并卸载；发布文件、环境配置、服务账号、日志、图片和数据库均保留。\n' "$unit_name"
    exit 0
fi

for command in getent groupadd useradd install chown chmod; do command -v "$command" >/dev/null || fail "缺少命令：$command"; done
[[ -e "$env_file" && ! -f "$env_file" ]] && fail '环境配置路径不是常规文件。'
[[ ! -L "$env_file" ]] || fail '环境配置不能使用符号链接。'
if [[ -f "$unit_path" ]]; then stop_owned_service; fi
getent group "$service_group" >/dev/null || groupadd --system "$service_group"
getent passwd "$service_user" >/dev/null || useradd --system --gid "$service_group" --home-dir "$script_dir" --shell /usr/sbin/nologin "$service_user"
# 服务只写入本发布目录；自定义外部存储路径需预先授予同一账号写入权限。
chown --recursive --no-dereference "$service_user:$service_group" -- "$script_dir"
chmod 750 -- "$script_dir"
chmod 750 -- "$binary"
for script in install.sh uninstall.sh service.sh; do chmod 750 -- "$script_dir/$script"; done
if [[ ! -f "$env_file" ]]; then
    install -d -m 755 -- "$(dirname -- "$env_file")"
    install -m 600 /dev/null "$env_file"
    printf '# 中文说明：持久化部署环境变量，修改后重新执行安装或重启服务。\n# ConnectionStrings__MySql="填写实际数据库连接"\n# Access__BootstrapKey="填写本次部署初始化密钥"\n' > "$env_file"
fi
chmod 600 -- "$env_file"
temporary_unit="$(mktemp "$unit_path.XXXXXX")"
trap 'rm -f -- "${temporary_unit:-}"' EXIT
cat > "$temporary_unit" <<EOF
# ZeyeHubInstallDirectory=$script_dir
[Unit]
Description=Zeye Sorting Hub
Wants=network-online.target
After=network-online.target
StartLimitIntervalSec=120
StartLimitBurst=5

[Service]
Type=notify
NotifyAccess=main
User=$service_user
Group=$service_group
WorkingDirectory=$(unit_quote "$script_dir" working_directory)
Environment=DOTNET_ENVIRONMENT=Production
EnvironmentFile=$(unit_quote "$env_file")
ExecStart=$(unit_quote "$binary" executable)
Restart=on-failure
RestartSec=10
TimeoutStartSec=$timeout_seconds
TimeoutStopSec=$timeout_seconds
KillSignal=SIGTERM
NoNewPrivileges=true
UMask=0027

[Install]
WantedBy=multi-user.target
EOF
chmod 644 -- "$temporary_unit"
mv -- "$temporary_unit" "$unit_path"
temporary_unit=''
systemctl daemon-reload
systemctl enable "$unit_name"
systemctl restart "$unit_name"
systemctl is-active --quiet "$unit_name" || fail '服务未保持运行，请检查 systemctl status 和 logs。'
wait_ready
printf '服务 %s 已安装并运行，已启用开机自动启动。\n默认入口：http://127.0.0.1:5078/；运行日志：%s/logs。\n' "$unit_name" "$script_dir"
