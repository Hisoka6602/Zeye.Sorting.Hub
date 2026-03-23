#!/usr/bin/env bash

set -euo pipefail

# 记录校验失败总数。
FAILURES=0

# Copilot 限制规则快照哈希；当规则文本变更时必须同步更新本脚本。
COPILOT_RULES_SHA256="c12fca7a1d694d2d7dad88c5d15a70ffd6be77885f44339ffaa0feb7c6cc906e"

# 记录当前执行阶段信息，便于 CI 日志定位。
log_step() {
  local message="$1"
  echo "[copilot-rules] $message"
}

# 统一处理失败计数与错误输出。
record_failure() {
  local message="$1"
  echo "::error::$message"
  FAILURES=$((FAILURES + 1))
}

# 将规则文本做轻量归一化，便于关键词匹配。
normalize_text() {
  local raw_text="$1"
  echo "$raw_text" | tr -d '[:space:]`'
}

# 计算规则集合哈希，保证规则变更会触发脚本同步更新。
compute_rules_hash() {
  local rules_text="$1"
  printf '%s' "$rules_text" | sha256sum | awk '{ print $1 }'
}

# 使用 grep 在受限环境中执行正则扫描。
search_matches() {
  local regex="$1"
  shift
  grep -R -n -E --exclude-dir=.git -- "$regex" "$@" 2>/dev/null || true
}

# 使用 find 获取文件列表并逐文件扫描。
search_matches_in_files() {
  local regex="$1"
  shift
  local files=("$@")
  local result=""
  local file_path=""
  for file_path in "${files[@]}"; do
    [[ -f "$file_path" ]] || continue
    local file_result
    file_result="$(grep -n -E -- "$regex" "$file_path" 2>/dev/null || true)"
    if [[ -n "$file_result" ]]; then
      result+="${file_path}:${file_result}"$'\n'
    fi
  done
  echo "$result"
}

# 列出 C# 与构建元数据文件。
list_csharp_and_build_files() {
  find . -type f \( -name '*.cs' -o -name '*.csx' -o -name '*.props' -o -name '*.targets' -o -name '*.csproj' \) | sort
}

# 检查代码中是否出现 UTC 相关 API。
check_no_utc_api_usage() {
  log_step "执行规则校验：禁止 UTC API"

  local utc_pattern='DateTime\\.UtcNow|DateTimeOffset\\.UtcNow|DateTimeKind\\.Utc|ToUniversalTime\\(|UtcDateTime|DateTimeStyles\\.AssumeUniversal|DateTimeStyles\\.AdjustToUniversal'
  local result
  local files
  mapfile -t files < <(list_csharp_and_build_files)
  result="$(search_matches_in_files "$utc_pattern" "${files[@]}")"
  if [[ -n "$result" ]]; then
    record_failure "检测到 UTC 相关 API，违反规则 1：\n$result"
  fi
}

# 检查配置时间示例是否包含 Z 或 offset。
check_no_utc_or_offset_datetime_examples_in_config() {
  log_step "执行规则校验：配置时间示例不得使用 Z 或 offset"

  local datetime_with_zone_pattern='"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9:\.]+(Z|[+-][0-9]{2}:[0-9]{2})"'
  local result
  result="$(find . -type f -name 'appsettings*.json' -print0 | xargs -0 grep -n -E -- "$datetime_with_zone_pattern" 2>/dev/null || true)"
  if [[ -n "$result" ]]; then
    record_failure "检测到带 Z/offset 的时间配置示例，违反规则 2：\n$result"
  fi
}

# 检查 PR 中新增/删除文件时是否同步修改 README。
check_readme_changed_when_files_added_or_deleted() {
  log_step "执行规则校验：新增/删除文件后需同步更新 README"

  if [[ "${GITHUB_EVENT_NAME:-}" != "pull_request" || -z "${GITHUB_BASE_REF:-}" ]]; then
    log_step "当前不是 pull_request 上下文，跳过规则 3 的 diff 校验"
    return
  fi

  if ! git fetch --no-tags --prune origin "${GITHUB_BASE_REF}:${GITHUB_BASE_REF}" >/dev/null 2>&1; then
    record_failure "无法拉取基线分支 origin/${GITHUB_BASE_REF}，无法执行规则 3 的 PR diff 校验。"
    return
  fi

  local diff_output
  diff_output="$(git --no-pager diff --name-status "origin/${GITHUB_BASE_REF}...HEAD")"

  local added_or_deleted
  added_or_deleted="$(echo "$diff_output" | awk '$1 == "A" || $1 == "D" { print $0 }')"

  if [[ -z "$added_or_deleted" ]]; then
    log_step "未检测到新增/删除文件，规则 3 通过"
    return
  fi

  local readme_touched
  readme_touched="$(echo "$diff_output" | awk '$2 == "README.md" { print $0 }')"
  if [[ -z "$readme_touched" ]]; then
    record_failure "检测到新增/删除文件，但 README.md 未同步修改，违反规则 3。"
  fi
}

# 检查枚举是否定义在 Domain/Enums 目录。
check_enum_location() {
  log_step "执行规则校验：枚举目录约束"

  local result
  result="$(find . -type f -name '*.cs' ! -path './.git/*' ! -path './Zeye.Sorting.Hub.Domain/Enums/*' -print0 | xargs -0 grep -n -E -- '^[[:space:]]*((public|internal|private|protected)[[:space:]]+)?(file[[:space:]]+)?enum[[:space:]]+' 2>/dev/null || true)"
  if [[ -n "$result" ]]; then
    record_failure "检测到不在 Zeye.Sorting.Hub.Domain/Enums 下的枚举定义，违反规则 8：\n$result"
  fi
}

# 检查枚举文件是否包含 Description 与注释。
check_enum_has_description_and_comments() {
  log_step "执行规则校验：枚举 Description 与注释约束"

  local enum_files=()
  mapfile -t enum_files < <(find ./Zeye.Sorting.Hub.Domain/Enums -type f -name '*.cs' 2>/dev/null | sort)
  if [[ "${#enum_files[@]}" -eq 0 ]]; then
    return
  fi

  local file_path=""
  for file_path in "${enum_files[@]}"; do
    [[ -z "$file_path" ]] && continue
    if ! grep -q -E '^[[:space:]]*((public|internal|private|protected)[[:space:]]+)?(file[[:space:]]+)?enum[[:space:]]+' "$file_path"; then
      continue
    fi

    if ! grep -q -E '\[Description\("' "$file_path"; then
      record_failure "枚举文件缺少 Description 特性，违反规则 9：$file_path"
    fi

    if ! grep -q -E '^[[:space:]]*///' "$file_path"; then
      record_failure "枚举文件缺少 XML 注释，违反规则 9：$file_path"
    fi
  done
}

# 检查事件载荷文件路径约束。
check_event_payload_location() {
  log_step "执行规则校验：事件载荷目录约束"

  local result
  result="$(find . -type f -name '*EventArgs.cs' ! -path '*/Events/*' ! -path './.git/*' | sort || true)"
  if [[ -n "$result" ]]; then
    record_failure "检测到不在 Events 子目录的事件载荷文件，违反规则 10：\n$result"
  fi
}

# 检查事件载荷类型约束。
check_event_payload_type() {
  log_step "执行规则校验：事件载荷类型约束"

  local event_files=()
  mapfile -t event_files < <(find . -type f -name '*EventArgs.cs' -path '*/Events/*' ! -path './.git/*' | sort)
  if [[ "${#event_files[@]}" -eq 0 ]]; then
    return
  fi

  local file_path=""
  for file_path in "${event_files[@]}"; do
    [[ -z "$file_path" ]] && continue
    if ! grep -q -E 'readonly[[:space:]]+record[[:space:]]+struct' "$file_path"; then
      record_failure "事件载荷未使用 readonly record struct，违反规则 11：$file_path"
    fi
  done
}

# 检查是否使用过时标记。
check_no_obsolete_attribute() {
  log_step "执行规则校验：禁止使用过时标记"

  local result
  result="$(find . -type f -name '*.cs' ! -path './.git/*' -print0 | xargs -0 grep -n -E -- '\[Obsolete(\(|\])' 2>/dev/null || true)"
  if [[ -n "$result" ]]; then
    record_failure "检测到 Obsolete 标记，违反规则 27：\n$result"
  fi
}

# 对非自动化规则做显式登记，避免漏检时静默通过。
acknowledge_manual_rule() {
  local rule_number="$1"
  log_step "规则 ${rule_number} 属于人工审查范围，当前 CI 仅做登记。"
}

# 判断规则是否属于人工审查范围。
is_manual_rule() {
  local normalized_text="$1"
  local manual_keywords=(
    "doc/pdf文档解析到md"
    "方法都需要有注释"
    "全局禁止代码重复"
    "小工具类尽量代码简洁"
    "所有的异常都必须输出日志"
    "Copilot的回答/描述/交流都需要使用中文"
    "所有的类的字段都必须有注释"
    "日志只能使用Nlog"
    "Copilot任务默认由Copilot创建拉取请求"
    "每次修改代码后都需要检查是否影分身代码"
    "严格划分结构层级边界"
    "性能更高的特性标记"
    "注释中禁止出现第二人称"
    "命名有严格要求"
    "历史更新记录不要写在README.md"
    "工具代码需要提取集中"
    "swagger的所有参数、方法、枚举项都必须要有中文注释"
    "每个类都需要独立的文件"
    "md文件除README.md外"
  )

  local keyword=""
  for keyword in "${manual_keywords[@]}"; do
    if [[ "$normalized_text" == *"$keyword"* ]]; then
      return 0
    fi
  done

  return 1
}

# 根据规则文本分派自动化校验或人工登记。
run_rule_by_text() {
  local rule_number="$1"
  local rule_text="$2"
  local normalized_text
  normalized_text="$(normalize_text "$rule_text")"

  if [[ "$normalized_text" == *"禁止使用UTC"* || "$normalized_text" == *"UTC相关API"* ]]; then
    check_no_utc_api_usage
    return
  fi

  if [[ "$normalized_text" == *"读取配置中的时间字符串"* || "$normalized_text" == *"示例配置不得使用Z或offset"* ]]; then
    check_no_utc_or_offset_datetime_examples_in_config
    return
  fi

  if [[ "$normalized_text" == *"新增文件或删除文件后"* && "$normalized_text" == *"README.md"* ]]; then
    check_readme_changed_when_files_added_or_deleted
    return
  fi

  if [[ "$normalized_text" == *"枚举都需要定义在"* && "$normalized_text" == *"Zeye.Sorting.Hub.Domain.Enums"* ]]; then
    check_enum_location
    return
  fi

  if [[ "$normalized_text" == *"枚举都必须包含"* && "$normalized_text" == *"Description"* ]]; then
    check_enum_has_description_and_comments
    return
  fi

  if [[ "$normalized_text" == *"事件载荷都必须定义在"* && "$normalized_text" == *"Events"* ]]; then
    check_event_payload_location
    return
  fi

  if [[ "$normalized_text" == *"事件载荷需要使用"* && "$normalized_text" == *"readonlyrecordstruct"* ]]; then
    check_event_payload_type
    return
  fi

  if [[ "$normalized_text" == *"禁止使用过时标记"* ]]; then
    check_no_obsolete_attribute
    return
  fi

  if is_manual_rule "$normalized_text"; then
    acknowledge_manual_rule "$rule_number"
    return
  fi

  record_failure "发现未映射的 Copilot 规则，请同步更新 CI 校验逻辑（规则 ${rule_number}: ${rule_text}）。"
}

# 读取 Copilot 限制规则并逐条执行对应校验。
run_all_rules() {
  local instructions_file=".github/copilot-instructions.md"
  if [[ ! -f "$instructions_file" ]]; then
    record_failure "未找到规则文件：$instructions_file"
    return
  fi

  log_step "加载规则文件：$instructions_file"

  local rules
  rules="$(awk '
    /^# Copilot 限制规则/ { in_rules=1; next }
    /^#/ && in_rules { in_rules=0; exit }
    /^# Copilot Repository Instructions/ { in_rules=0; exit }
    in_rules && /^[0-9]+\./ {
      line = $0
      sub(/^[0-9]+\.[[:space:]]*/, "", line)
      number = $0
      sub(/\..*$/, "", number)
      print number "\t" line
    }
  ' "$instructions_file")"

  if [[ -z "$rules" ]]; then
    record_failure "未从 $instructions_file 解析到 Copilot 限制规则。"
    return
  fi

  local current_rules_hash
  current_rules_hash="$(compute_rules_hash "$rules")"
  if [[ "$current_rules_hash" != "$COPILOT_RULES_SHA256" ]]; then
    record_failure "检测到 copilot-instructions 规则文本发生变化（hash=$current_rules_hash），请同步更新 .github/scripts/validate-copilot-rules.sh 的规则映射与 COPILOT_RULES_SHA256。"
    return
  fi

  while IFS=$'\t' read -r rule_number rule_text; do
    [[ -z "$rule_number" || -z "$rule_text" ]] && continue
    log_step "处理规则 ${rule_number}: ${rule_text}"
    run_rule_by_text "$rule_number" "$rule_text"
  done <<< "$rules"
}

run_all_rules

if [[ "$FAILURES" -gt 0 ]]; then
  log_step "校验完成：发现 ${FAILURES} 个问题。"
  exit 1
fi

log_step "校验完成：全部通过。"
