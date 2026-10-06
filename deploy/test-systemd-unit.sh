#!/usr/bin/env bash
# 中文说明：使用真实 systemd 解析器验证安装脚本生成的 unit，不注册服务或创建账号。
set -euo pipefail
repo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
source_script="$repo_dir/Zeye.Sorting.Hub.Host/service.sh"
command -v systemd-analyze >/dev/null || { printf '需要安装 systemd-analyze。\n' >&2; exit 1; }
test_root="$(mktemp -d /tmp/zeye-systemd-unit-check.XXXXXX)"
unit_name='zeye-unit-verification.service'
temporary_unit="$test_root/$unit_name"
script_dir=''
env_file=''
binary=''
# 中文说明：只删除本次测试创建的具体文件，目录为空后再移除。
cleanup() {
    rm -f -- "$temporary_unit"
    if [[ -n "$env_file" ]]; then rm -f -- "$env_file"; fi
    if [[ -n "$binary" ]]; then rm -f -- "$binary"; fi
    if [[ -n "$script_dir" ]]; then
        rm -f -- "$script_dir/service.sh" "$script_dir/appsettings.json" "$script_dir/wwwroot/index.html"
        [[ ! -d "$script_dir/wwwroot" ]] || rmdir -- "$script_dir/wwwroot"
        rmdir -- "$script_dir"
    fi
    rmdir -- "$test_root"
}
trap cleanup EXIT
# 中文说明：直接复用当前安装实现的路径函数及 unit 模板，避免单独维护一份测试模板。
path_function="$(sed -n '/^unit_quote() {/,/^}/p' "$source_script")"
unit_template="$(sed -n '/^cat > "\$temporary_unit" <<EOF$/,/^EOF$/p' "$source_script")"
[[ -n "$path_function" && -n "$unit_template" ]] || { printf '安装实现缺少预期的 unit 生成入口。\n' >&2; exit 1; }
eval "$path_function"
service_user="$(id -un)"
service_group="$(id -gn)"
timeout_seconds=30
checks=0
for directory in 'default' '中文 发布目录' 'space dollar$ percent% path'; do
    script_dir="$test_root/$directory"
    env_file="$script_dir/service environment \$%.conf"
    binary="$script_dir/Zeye.Sorting.Hub.Host"
    mkdir -- "$script_dir"
    cp -- /bin/true "$binary"
    printf 'NativeVerification__Marker=unit-check\n' > "$env_file"
    eval "$unit_template"
    systemd-analyze verify "$temporary_unit"
    checks=$((checks + 1))
    rm -f -- "$env_file" "$binary"
    rmdir -- "$script_dir"
    script_dir=''; env_file=''; binary=''
done
for directory in 'quote" path' 'backslash\ path'; do
    script_dir="$test_root/$directory"
    binary="$script_dir/Zeye.Sorting.Hub.Host"
    mkdir -p -- "$script_dir/wwwroot"
    cp -- /bin/true "$binary"
    cp -- "$source_script" "$script_dir/service.sh"
    touch -- "$script_dir/appsettings.json" "$script_dir/wwwroot/index.html"
    if output="$(bash "$script_dir/service.sh" install --dry-run 2>&1)"; then
        printf '不支持的路径没有被安装预检拒绝：%s\n' "$directory" >&2; exit 1
    fi
    [[ "$output" == *'systemd 可执行文件路径'* ]]
    checks=$((checks + 1))
    rm -f -- "$binary" "$script_dir/service.sh" "$script_dir/appsettings.json" "$script_dir/wwwroot/index.html"
    rmdir -- "$script_dir/wwwroot" "$script_dir"
    script_dir=''; binary=''
done
printf '%s 项真实 systemd 解析及路径边界检查通过；未安装或启动服务。\n' "$checks"
