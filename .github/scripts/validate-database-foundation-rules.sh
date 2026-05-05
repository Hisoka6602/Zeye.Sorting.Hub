#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

cd "${REPO_ROOT}"

FAILURES=0
METHOD_SIGNATURE_PATTERN='^[[:space:]]*(public|internal|private|protected)[[:space:]]+(static[[:space:]]+)?(async[[:space:]]+)?([A-Za-z_][A-Za-z0-9_<>,\?\[\]\.]*)[[:space:]]+[A-Za-z_][A-Za-z0-9_]*[[:space:]]*\([^;]*\)[[:space:]]*(\{|=>)'

log_step() {
  local message="$1"
  echo "[database-foundation] ${message}"
}

record_failure() {
  local message="$1"
  echo "::error::${message}"
  FAILURES=$((FAILURES + 1))
}

is_pull_request_context() {
  [[ "${GITHUB_EVENT_NAME:-}" == "pull_request" && -n "${GITHUB_BASE_REF:-}" ]]
}

ensure_base_ref_ready() {
  if ! is_pull_request_context; then
    return
  fi

  if git show-ref --verify --quiet "refs/remotes/origin/${GITHUB_BASE_REF}"; then
    return
  fi

  git fetch --no-tags --prune origin "${GITHUB_BASE_REF}:refs/remotes/origin/${GITHUB_BASE_REF}" >/dev/null 2>&1 || true
}

list_changed_name_status() {
  if is_pull_request_context; then
    ensure_base_ref_ready
    git --no-pager diff --name-status "origin/${GITHUB_BASE_REF}...HEAD" || true
    return
  fi

  git --no-pager diff --name-status HEAD || true
}

list_changed_files() {
  local diff_filter="${1:-AM}"

  if is_pull_request_context; then
    ensure_base_ref_ready
    git --no-pager diff --name-only --diff-filter="${diff_filter}" "origin/${GITHUB_BASE_REF}...HEAD" || true
    return
  fi

  git --no-pager diff --name-only --diff-filter="${diff_filter}" HEAD || true
}

collect_changed_files() {
  local diff_filter="$1"
  shift
  local patterns=("$@")
  local path=""

  while IFS= read -r path; do
    [[ -z "${path}" ]] && continue
    [[ -f "${path}" ]] || continue

    local pattern=""
    for pattern in "${patterns[@]}"; do
      if [[ "${path}" == ${pattern} ]]; then
        echo "${path}"
        break
      fi
    done
  done < <(list_changed_files "${diff_filter}")
}

has_allowed_placeholder() {
  local line="$1"
  echo "${line}" | grep -Eq '<[^>]+>' && return 0
  echo "${line}" | grep -Eq '\$\{\{[^}]+\}\}' && return 0
  echo "${line}" | grep -Eq '\$\{[A-Za-z_][A-Za-z0-9_]*\}' && return 0
  echo "${line}" | grep -Eq '%[A-Za-z_][A-Za-z0-9_]*%' && return 0
  return 1
}

check_no_utc_usage() {
  log_step "检查禁止 UTC API 与配置时区后缀"

  local utc_pattern='DateTime\.UtcNow|DateTimeOffset\.UtcNow|DateTimeKind\.Utc|ToUniversalTime\(|UtcDateTime|DateTimeStyles\.AssumeUniversal|DateTimeStyles\.AdjustToUniversal'
  local config_pattern='"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9:\.]+(Z|[+-][0-9]{2}:[0-9]{2})"'
  local files=()
  local file=""

  while IFS= read -r file; do
    files+=("${file}")
  done < <(collect_changed_files "AM" "*.cs" "*.csproj" "*.props" "*.targets")

  if [[ "${#files[@]}" -gt 0 ]]; then
    local result=""
    result="$(grep -nE "${utc_pattern}" "${files[@]}" 2>/dev/null || true)"
    if [[ -n "${result}" ]]; then
      record_failure "检测到 UTC 相关 API：${result//$'\n'/' | '}"
    fi
  fi

  local config_files=()
  while IFS= read -r file; do
    config_files+=("${file}")
  done < <(collect_changed_files "AM" "*/appsettings*.json" "appsettings*.json")

  if [[ "${#config_files[@]}" -eq 0 ]]; then
    return
  fi

  local config_result=""
  config_result="$(grep -nE "${config_pattern}" "${config_files[@]}" 2>/dev/null || true)"
  if [[ -n "${config_result}" ]]; then
    record_failure "检测到配置时间示例包含 Z 或 offset：${config_result//$'\n'/' | '}"
  fi
}

check_readme_file_tree() {
  log_step "检查 README 文件树与职责说明是否已同步"

  local readme_path="README.md"
  local status_line=""

  while IFS= read -r status_line; do
    [[ -z "${status_line}" ]] && continue

    local status
    status="$(echo "${status_line}" | awk '{ print $1 }')"
    local path
    path="$(echo "${status_line}" | cut -f2-)"
    local file_name
    file_name="$(basename "${path}")"

    if [[ "${status}" == "A" ]]; then
      if ! grep -Fq "\`${file_name}\`" "${readme_path}"; then
        record_failure "README.md 未同步新增文件职责：${path}"
      fi
    elif [[ "${status}" == "D" ]]; then
      if grep -Fq "\`${file_name}\`" "${readme_path}"; then
        record_failure "README.md 仍保留已删除文件职责：${path}"
      fi
    fi
  done < <(list_changed_name_status)
}

check_sensitive_config() {
  log_step "检查新增或修改文件中的敏感配置"

  local files=()
  local file=""
  while IFS= read -r file; do
    files+=("${file}")
  done < <(collect_changed_files "AM" "*.json" "*.yml" "*.yaml" "*.cs" "*.csproj" "*.props" "*.targets" "*.sh")

  if [[ "${#files[@]}" -eq 0 ]]; then
    return
  fi

  local pattern='pwd=|password=|user id=sa|uid=root|accesskey|secretkey|token='
  local result=""
  local entry=""

  result="$(grep -niE "${pattern}" "${files[@]}" 2>/dev/null || true)"
  while IFS= read -r entry; do
    [[ -z "${entry}" ]] && continue
    local line_content
    line_content="$(echo "${entry}" | cut -d: -f3-)"

    if has_allowed_placeholder "${line_content}"; then
      continue
    fi

    if echo "${line_content}" | grep -Eq 'Password=<请通过环境变量注入>'; then
      continue
    fi

    record_failure "检测到敏感配置或高风险默认凭据：${entry}"
  done <<< "${result}"
}

check_no_shadow_code() {
  log_step "检查影分身代码（同文件重复方法签名）"

  local files=()
  local file=""
  while IFS= read -r file; do
    files+=("${file}")
  done < <(collect_changed_files "AM" "*.cs")

  if [[ "${#files[@]}" -eq 0 ]]; then
    return
  fi

  local duplicated=""
  local current_file=""
  for current_file in "${files[@]}"; do
    local duplicates
    duplicates="$(grep -nE "${METHOD_SIGNATURE_PATTERN}" "${current_file}" 2>/dev/null | sed -E 's/^[0-9]+://' | sed -E 's/[[:space:]]+/ /g' | sort | uniq -d || true)"
    if [[ -n "${duplicates}" ]]; then
      duplicated+="${current_file}: ${duplicates}"$'\n'
    fi
  done

  if [[ -n "${duplicated}" ]]; then
    record_failure "检测到同文件重复方法签名：${duplicated//$'\n'/' | '}"
  fi
}

check_enum_description() {
  log_step "检查新增或修改枚举的 Description 完整性"

  local files=()
  local file=""
  while IFS= read -r file; do
    files+=("${file}")
  done < <(collect_changed_files "AM" "*.cs")

  if [[ "${#files[@]}" -eq 0 ]]; then
    return
  fi

  local current_file=""
  for current_file in "${files[@]}"; do
    if ! grep -qE 'enum[[:space:]]+[A-Za-z_][A-Za-z0-9_]*' "${current_file}"; then
      continue
    fi

    if ! awk '
      BEGIN {
        insideEnum = 0
        braceDepth = 0
        hasDescription = 0
        failure = 0
      }
      {
        line = $0

        if (line ~ /enum[[:space:]]+[A-Za-z_][A-Za-z0-9_]*/) {
          insideEnum = 1
        }

        if (!insideEnum) {
          next
        }

        openCount = gsub(/\{/, "{", line)
        closeCount = gsub(/\}/, "}", line)
        braceDepth += openCount - closeCount

        if ($0 ~ /\[Description\(".*"\)\]/) {
          hasDescription = 1
          next
        }

        if ($0 ~ /^[[:space:]]*$/ || $0 ~ /^[[:space:]]*\/\// || $0 ~ /^[[:space:]]*\/\// || $0 ~ /^[[:space:]]*\/\*\*?/ || $0 ~ /^[[:space:]]*\*/ || $0 ~ /^[[:space:]]*/// || $0 ~ /^[[:space:]]*\[/) {
          next
        }

        if (braceDepth > 0 && $0 ~ /^[[:space:]]*[A-Za-z_][A-Za-z0-9_]*([[:space:]]*=.+)?[[:space:]]*,?[[:space:]]*$/) {
          if (hasDescription == 0) {
            failure = 1
            exit 1
          }
          hasDescription = 0
        }

        if (insideEnum && braceDepth <= 0) {
          insideEnum = 0
          hasDescription = 0
        }
      }
      END {
        exit failure
      }
    ' "${current_file}"; then
      record_failure "枚举项缺少 [Description(\"中文说明\")]：${current_file}"
    fi
  done
}

check_hosted_service_exception_handling() {
  log_step "检查 HostedService 异常捕获"

  local file=""
  while IFS= read -r file; do
    [[ -z "${file}" ]] && continue
    if ! grep -qE 'BackgroundService|IHostedService|ExecuteAsync' "${file}"; then
      continue
    fi

    if ! grep -q 'catch (Exception' "${file}"; then
      record_failure "HostedService 缺少 Exception 捕获与日志隔离：${file}"
    fi
  done < <(collect_changed_files "AM" "*HostedService*.cs" "*/HostedServices/*.cs")
}

check_background_loop_cancellation() {
  log_step "检查后台循环 CancellationToken 支持"

  local file=""
  while IFS= read -r file; do
    [[ -z "${file}" ]] && continue

    if ! grep -q 'while (' "${file}"; then
      continue
    fi

    if ! grep -q 'CancellationToken' "${file}"; then
      record_failure "后台循环文件未声明 CancellationToken：${file}"
      continue
    fi

    if ! grep -qE 'IsCancellationRequested|WaitForNextTickAsync\([^)]*Token|Delay\([^)]*Token' "${file}"; then
      record_failure "后台循环未消费 CancellationToken：${file}"
    fi
  done < <(collect_changed_files "AM" "*HostedService*.cs" "*/HostedServices/*.cs")
}

check_bounded_capacity() {
  log_step "检查批量写入与后台队列是否为有界容量"

  local file=""
  while IFS= read -r file; do
    [[ -z "${file}" ]] && continue

    if ! grep -qE 'Channel\.Create|CreateUnbounded|CreateBounded|Queue<' "${file}"; then
      continue
    fi

    if ! grep -qE 'CreateBounded|BoundedChannelOptions|Capacity' "${file}"; then
      record_failure "检测到疑似无界批量写入或后台队列实现：${file}"
    fi
  done < <(collect_changed_files "AM" "*WriteBuffer*.cs" "*Buffer*.cs" "*Queue*.cs" "*Channel*.cs" "*/WriteBuffering/*.cs")
}

run_advanced_checks() {
  check_enum_description
  check_hosted_service_exception_handling
  check_background_loop_cancellation
  check_bounded_capacity
}

run_all_checks() {
  check_no_utc_usage
  check_readme_file_tree
  check_sensitive_config
  check_no_shadow_code
  run_advanced_checks
}

main() {
  local mode="all"

  if [[ "${1:-}" == "--check" ]]; then
    mode="${2:-all}"
  fi

  case "${mode}" in
    no-utc)
      check_no_utc_usage
      ;;
    readme-file-tree)
      check_readme_file_tree
      ;;
    sensitive-config)
      check_sensitive_config
      ;;
    no-shadow-code)
      check_no_shadow_code
      ;;
    advanced)
      run_advanced_checks
      ;;
    all)
      run_all_checks
      ;;
    *)
      echo "未知检查模式：${mode}" >&2
      exit 1
      ;;
  esac

  if [[ "${FAILURES}" -gt 0 ]]; then
    echo "[database-foundation] 校验失败，总计 ${FAILURES} 项。"
    exit 1
  fi

  echo "[database-foundation] 校验通过。"
}

main "$@"
