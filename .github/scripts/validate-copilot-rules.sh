#!/usr/bin/env bash

set -euo pipefail

# 记录校验失败总数。
FAILURES=0

# Copilot 限制规则快照哈希；规则变更后运行脚本内 compute_rules_hash 对应逻辑重新计算并更新此值。
COPILOT_RULES_SHA256="9350b799b04537e6b6a17acb212af3a936203db92ac4daf0d293e779cb14d521"

# PR diff 缓存，避免重复计算。
PR_DIFF_READY=0
PR_DIFF_NAME_ONLY=""
PR_DIFF_NAME_STATUS=""

# 方法声明正则（用于注释检查与重复签名检查复用）。
METHOD_SIGNATURE_PATTERN='^[[:space:]]*(public|internal|private|protected)[[:space:]]+(static[[:space:]]+)?(async[[:space:]]+)?([A-Za-z_][A-Za-z0-9_<>,\?\[\]\.]*)[[:space:]]+[A-Za-z_][A-Za-z0-9_]*[[:space:]]*\([^;]*\)[[:space:]]*(\{|=>)'
# 字段声明正则（用于字段注释检查）。
FIELD_SIGNATURE_PATTERN='^[[:space:]]*(private|protected|internal|public)[[:space:]]+(static[[:space:]]+)?(readonly[[:space:]]+)?[A-Za-z_][A-Za-z0-9_<>,\?\[\]\.]*[[:space:]]+[A-Za-z_][A-Za-z0-9_]*[[:space:]]*(=|;)'
# Swagger 相关改动路径模式。
SWAGGER_RELATED_PATH_PATTERN='(^|/)(Swagger/|Program\.cs|Zeye\.Sorting\.Hub\.Contracts/Enums/|Zeye\.Sorting\.Hub\.Domain/Enums/)'

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

# 判断是否处于 pull_request 事件上下文。
is_pull_request_context() {
  [[ "${GITHUB_EVENT_NAME:-}" == "pull_request" && -n "${GITHUB_BASE_REF:-}" ]]
}

# 准备 PR diff 缓存。
ensure_pr_diff_ready() {
  if [[ "$PR_DIFF_READY" -eq 1 ]]; then
    return
  fi

  if ! is_pull_request_context; then
    PR_DIFF_READY=1
    PR_DIFF_NAME_ONLY=""
    PR_DIFF_NAME_STATUS=""
    return
  fi

  if ! git fetch --no-tags --prune origin "${GITHUB_BASE_REF}:${GITHUB_BASE_REF}" >/dev/null 2>&1; then
    record_failure "无法拉取基线分支 origin/${GITHUB_BASE_REF}，无法执行基于 PR diff 的规则校验。"
    PR_DIFF_READY=1
    PR_DIFF_NAME_ONLY=""
    PR_DIFF_NAME_STATUS=""
    return
  fi

  PR_DIFF_NAME_ONLY="$(git --no-pager diff --name-only "origin/${GITHUB_BASE_REF}...HEAD")"
  PR_DIFF_NAME_STATUS="$(git --no-pager diff --name-status "origin/${GITHUB_BASE_REF}...HEAD")"
  PR_DIFF_READY=1
}

# 获取 PR 变更文件（按后缀过滤）。
get_pr_changed_files_by_suffix() {
  local suffix="$1"
  ensure_pr_diff_ready
  if ! is_pull_request_context || [[ -z "$PR_DIFF_NAME_ONLY" ]]; then
    return
  fi
  echo "$PR_DIFF_NAME_ONLY" | grep -E "\.${suffix}$" || true
}

# 获取 PR 新增文件（按后缀过滤）。
get_pr_added_files_by_suffix() {
  local suffix="$1"
  ensure_pr_diff_ready
  if ! is_pull_request_context || [[ -z "$PR_DIFF_NAME_STATUS" ]]; then
    return
  fi
  echo "$PR_DIFF_NAME_STATUS" | awk '$1 == "A" { print $2 }' | grep -E "\.${suffix}$" || true
}

# 获取 PR 新增或删除文件明细。
get_pr_added_or_deleted_files() {
  ensure_pr_diff_ready
  if ! is_pull_request_context || [[ -z "$PR_DIFF_NAME_STATUS" ]]; then
    return
  fi
  echo "$PR_DIFF_NAME_STATUS" | awk '$1 == "A" || $1 == "D" { print $0 }'
}

# 判断文件是否包含中文字符。
contains_chinese_char() {
  local input="$1"
  if echo "$input" | grep -q -P '\p{Han}' 2>/dev/null; then
    return 0
  fi
  echo "$input" | grep -q -E '[一-龥]'
}

# 判断声明前是否存在紧邻的 XML 注释块（允许空行与特性行）。
has_adjacent_xml_comment() {
  local file_path="$1"
  local target_line="$2"
  local max_steps="${3:-10}"
  local current_line=$((target_line - 1))
  local steps=0

  while [[ "$current_line" -ge 1 && "$steps" -lt "$max_steps" ]]; do
    local content
    content="$(sed -n "${current_line}p" "$file_path")"

    if echo "$content" | grep -q -E '^[[:space:]]*$|^[[:space:]]*\[[^]]+\][[:space:]]*$'; then
      current_line=$((current_line - 1))
      steps=$((steps + 1))
      continue
    fi

    if echo "$content" | grep -q -E '^[[:space:]]*///'; then
      return 0
    fi

    return 1
  done

  return 1
}

# 抽取从起始行到匹配右花括号结束的代码块。
extract_brace_block() {
  local file_path="$1"
  local start_line="$2"
  awk -v start="$start_line" '
    NR < start { next }
    {
      line = $0
      open_count = gsub(/\{/, "{", line)
      close_count = gsub(/\}/, "}", line)
      if (open_count > 0) {
        saw_open = 1
      }
      balance += (open_count - close_count)
      print $0
      if (saw_open && balance <= 0) {
        exit
      }
    }
  ' "$file_path"
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

  local datetime_with_zone_pattern='"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9:\\.]+(Z|[+-][0-9]{2}:[0-9]{2})"'
  local result
  result="$(find . -type f -name 'appsettings*.json' -print0 | xargs -0 grep -n -E -- "$datetime_with_zone_pattern" 2>/dev/null || true)"
  if [[ -n "$result" ]]; then
    record_failure "检测到带 Z/offset 的时间配置示例，违反规则 2：\n$result"
  fi
}

# 检查 doc/pdf 解析产物的可追溯出处说明。
check_doc_pdf_to_md_traceability() {
  log_step "执行规则校验：doc/pdf 解析到 md 需可追溯出处"
  if ! is_pull_request_context; then
    log_step "当前不是 pull_request 上下文，跳过规则 4 的增量校验"
    return
  fi

  local changed_md_files
  changed_md_files="$(get_pr_changed_files_by_suffix "md")"
  if [[ -z "$changed_md_files" ]]; then
    return
  fi

  local file_path=""
  while IFS= read -r file_path; do
    [[ -z "$file_path" ]] && continue
    if [[ "$file_path" == "README.md" || "$file_path" == ".github/copilot-instructions.md" ]]; then
      continue
    fi

    local has_traceability=0
    if grep -q -E '^[[:space:]]*(#+[[:space:]]*)?(出处|来源|原文|原文档)[[:space:]:：]|^[[:space:]]*[-*][[:space:]]*(出处|来源|原文|原文档)[[:space:]:：]|^[[:space:]]*(Source|source)[[:space:]:：]' "$file_path"; then
      has_traceability=1
    fi

    if [[ "$has_traceability" -eq 0 ]]; then
      record_failure "Markdown 文件缺少出处/来源标记，违反规则 4：$file_path"
    fi
  done <<< "$changed_md_files"
}

# 检查新增/修改方法是否具备注释。
check_method_comments() {
  log_step "执行规则校验：方法注释覆盖"
  if ! is_pull_request_context; then
    log_step "当前不是 pull_request 上下文，跳过规则 5 的增量校验"
    return
  fi

  local changed_cs_files
  changed_cs_files="$(get_pr_changed_files_by_suffix "cs")"
  if [[ -z "$changed_cs_files" ]]; then
    return
  fi

  local file_path=""
  while IFS= read -r file_path; do
    [[ -z "$file_path" ]] && continue
    [[ -f "$file_path" ]] || continue

    local line_number=""
    while IFS=: read -r line_number _; do
      [[ -z "$line_number" ]] && continue
      if ! has_adjacent_xml_comment "$file_path" "$line_number" 12; then
        record_failure "方法缺少 XML 注释，违反规则 5：${file_path}:${line_number}"
      fi
    done < <(grep -n -E "$METHOD_SIGNATURE_PATTERN" "$file_path" || true)
  done <<< "$changed_cs_files"
}

# 检查重复代码风险（简单硬门禁：同文件重复方法签名）。
check_no_duplicate_method_signature() {
  log_step "执行规则校验：重复方法签名检查"

  local cs_files=""
  if is_pull_request_context; then
    cs_files="$(get_pr_changed_files_by_suffix "cs")"
  else
    cs_files="$(find . -type f -name '*.cs' ! -path './.git/*' ! -path '*/bin/*' ! -path '*/obj/*')"
  fi

  if [[ -z "$cs_files" ]]; then
    return
  fi

  local duplicated
  duplicated="$(while IFS= read -r f; do
    (grep -E "$METHOD_SIGNATURE_PATTERN" "$f" || true) \
      | sed -E 's/[[:space:]]+/ /g' \
      | sort \
      | uniq -d \
      | sed "s#^#${f}: #"
  done <<< "$cs_files" | sed '/^[[:space:]]*$/d' || true)"

  if [[ -n "$duplicated" ]]; then
    record_failure "检测到同文件重复方法签名，违反规则 6：\n$duplicated"
  fi
}

# 检查工具类命名及实现约束。
check_tool_class_quality() {
  log_step "执行规则校验：工具类命名与复用约束"

  if ! is_pull_request_context; then
    log_step "当前不是 pull_request 上下文，跳过规则 7 的增量校验"
    return
  fi

  local added_cs_files
  added_cs_files="$(get_pr_added_files_by_suffix "cs")"
  if [[ -z "$added_cs_files" ]]; then
    return
  fi

  local forbidden_tool_names
  forbidden_tool_names="$(echo "$added_cs_files" | grep -E '(Helper|Wrapper|Adapter|Facade|Manager)\.cs$' || true)"
  if [[ -n "$forbidden_tool_names" ]]; then
    record_failure "检测到禁用工具类命名，违反规则 7：\n$forbidden_tool_names"
  fi
}

# 检查 PR 中新增/删除文件时是否同步修改 README。
check_readme_changed_when_files_added_or_deleted() {
  log_step "执行规则校验：新增/删除文件后需同步更新 README"

  if ! is_pull_request_context; then
    log_step "当前不是 pull_request 上下文，跳过规则 3 的 diff 校验"
    return
  fi

  ensure_pr_diff_ready

  local added_or_deleted
  added_or_deleted="$(echo "$PR_DIFF_NAME_STATUS" | awk '$1 == "A" || $1 == "D" { print $0 }')"

  if [[ -z "$added_or_deleted" ]]; then
    log_step "未检测到新增/删除文件，规则 3 通过"
    return
  fi

  local readme_touched
  readme_touched="$(echo "$PR_DIFF_NAME_STATUS" | awk '$2 == "README.md" { print $0 }')"
  if [[ -z "$readme_touched" ]]; then
    record_failure "检测到新增/删除文件，但 README.md 未同步修改，违反规则 3。"
  fi
}

# 检查枚举是否定义在 Domain/Enums 目录。
check_enum_location() {
  log_step "执行规则校验：枚举目录约束"

  local result
  result="$(
    find . -type f -name '*.cs' ! -path './.git/*' -print0 \
      | xargs -0 grep -n -E -- '^[[:space:]]*((public|internal|private|protected)[[:space:]]+)?(file[[:space:]]+)?enum[[:space:]]+' 2>/dev/null \
      | awk -F: '$1 !~ /^\.\/Zeye\.Sorting\.Hub\.Domain\/Enums\//' \
      || true
  )"
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

# 检查异常处理是否输出日志（增量：新增 catch 必须含 Log 调用）。
check_exception_logging() {
  log_step "执行规则校验：异常必须输出日志"
  if ! is_pull_request_context; then
    log_step "当前不是 pull_request 上下文，跳过规则 12 的增量校验"
    return
  fi

  local changed_cs_files
  changed_cs_files="$(get_pr_changed_files_by_suffix "cs")"
  if [[ -z "$changed_cs_files" ]]; then
    return
  fi

  local file_path=""
  while IFS= read -r file_path; do
    [[ -z "$file_path" ]] && continue
    [[ -f "$file_path" ]] || continue

    local catch_lines
    catch_lines="$(grep -n -E '^[[:space:]]*catch[[:space:]]*(\(|\{)' "$file_path" || true)"
    if [[ -z "$catch_lines" ]]; then
      continue
    fi

    local line_no=""
    while IFS=: read -r line_no _; do
      [[ -z "$line_no" ]] && continue
      local block
      block="$(extract_brace_block "$file_path" "$line_no")"
      if ! echo "$block" | grep -q -E '(^|[[:space:]])(Log|Logger|_?logger|NLogLogger)\.(Error|Warn|Info|Debug|Fatal|Trace)\('; then
        record_failure "catch 块缺少日志输出，违反规则 12：${file_path}:${line_no}"
      fi
    done <<< "$catch_lines"
  done <<< "$changed_cs_files"
}

# 检查类字段是否具备注释（增量）。
check_field_comments() {
  log_step "执行规则校验：类字段注释覆盖"
  if ! is_pull_request_context; then
    log_step "当前不是 pull_request 上下文，跳过规则 14 的增量校验"
    return
  fi

  local changed_cs_files
  changed_cs_files="$(get_pr_changed_files_by_suffix "cs")"
  if [[ -z "$changed_cs_files" ]]; then
    return
  fi

  local file_path=""
  while IFS= read -r file_path; do
    [[ -z "$file_path" ]] && continue
    [[ -f "$file_path" ]] || continue

    local line_number=""
    while IFS=: read -r line_number _; do
      [[ -z "$line_number" ]] && continue
      if ! has_adjacent_xml_comment "$file_path" "$line_number" 10; then
        record_failure "字段缺少 XML 注释，违反规则 14：${file_path}:${line_number}"
      fi
    done < <(grep -n -E "$FIELD_SIGNATURE_PATTERN" "$file_path" || true)
  done <<< "$changed_cs_files"
}

# 检查日志框架仅使用 NLog（增量）。
check_nlog_only() {
  log_step "执行规则校验：日志仅使用 NLog"
  if ! is_pull_request_context; then
    log_step "当前不是 pull_request 上下文，跳过规则 15 的增量校验"
    return
  fi

  local changed_cs_files
  changed_cs_files="$(get_pr_changed_files_by_suffix "cs")"
  if [[ -z "$changed_cs_files" ]]; then
    return
  fi

  local file_path=""
  while IFS= read -r file_path; do
    [[ -z "$file_path" ]] && continue
    [[ -f "$file_path" ]] || continue
    if grep -q -E 'using[[:space:]]+Microsoft\.Extensions\.Logging|ILogger[[:space:]]*<' "$file_path"; then
      record_failure "检测到非 NLog 日志用法，违反规则 15：$file_path"
    fi
  done <<< "$changed_cs_files"
}

# 检查注释中是否包含第二人称（增量）。
check_no_second_person_in_comments() {
  log_step "执行规则校验：注释禁止第二人称"
  if ! is_pull_request_context; then
    log_step "当前不是 pull_request 上下文，跳过规则 20 的增量校验"
    return
  fi

  local changed_cs_files
  changed_cs_files="$(get_pr_changed_files_by_suffix "cs")"
  if [[ -z "$changed_cs_files" ]]; then
    return
  fi

  local second_person_cn_pattern='你|您|你们|您们'
  local second_person_en_pattern='\<(you|your|yours)\>'
  local file_path=""
  while IFS= read -r file_path; do
    [[ -z "$file_path" ]] && continue
    [[ -f "$file_path" ]] || continue
    local result
    result="$(
      awk -v pattern_cn="$second_person_cn_pattern" -v pattern_en="$second_person_en_pattern" '
        BEGIN { in_block = 0 }
        {
          line = $0
          comment = ""

          if (in_block) {
            comment = line
            end_pos = index(line, "*/")
            if (end_pos > 0) {
              in_block = 0
              comment = substr(line, 1, end_pos + 1)
            }
          }

          if (!in_block) {
            line_comment_pos = index(line, "//")
            block_comment_pos = index(line, "/*")
            if (line_comment_pos > 0 && (block_comment_pos == 0 || line_comment_pos < block_comment_pos)) {
              comment = substr(line, line_comment_pos)
            } else if (block_comment_pos > 0) {
              in_block = 1
              comment = substr(line, block_comment_pos)
              end_pos = index(comment, "*/")
              if (end_pos > 0) {
                in_block = 0
                comment = substr(comment, 1, end_pos + 1)
              }
            }
          }

          if (comment != "") {
            lower_comment = tolower(comment)
            if (lower_comment ~ pattern_en || comment ~ pattern_cn) {
              printf("%d:%s\n", NR, comment)
            }
          }
        }
      ' "$file_path"
    )"
    if [[ -n "$result" ]]; then
      record_failure "注释存在第二人称，违反规则 20：\n${file_path}:\n${result}"
    fi
  done <<< "$changed_cs_files"
}

# 检查 README 不应承载历史更新记录。
check_no_history_in_readme() {
  log_step "执行规则校验：README 禁止历史更新记录"
  if ! is_pull_request_context; then
    log_step "当前不是 pull_request 上下文，跳过规则 22 的增量校验"
    return
  fi

  ensure_pr_diff_ready
  if ! echo "$PR_DIFF_NAME_ONLY" | grep -q -E '^README\.md$'; then
    return
  fi

  local added_lines
  added_lines="$(git --no-pager diff --unified=0 "origin/${GITHUB_BASE_REF}...HEAD" -- README.md | grep -E '^\+' | grep -v -E '^\+\+\+' || true)"
  if echo "$added_lines" | grep -q -E '更新记录（CHANGELOG）|历史更新记录'; then
    record_failure "README.md 新增了历史更新记录相关内容，违反规则 22。"
  fi
}

# 检查工具代码集中复用（硬门禁：禁止新增重复 Guard/Helper 语义文件）。
check_tool_code_centralization() {
  log_step "执行规则校验：同义工具代码集中复用"
  if ! is_pull_request_context; then
    log_step "当前不是 pull_request 上下文，跳过规则 23 的增量校验"
    return
  fi

  local added_cs_files
  added_cs_files="$(get_pr_added_files_by_suffix "cs")"
  if [[ -z "$added_cs_files" ]]; then
    return
  fi

  local file_path=""
  while IFS= read -r file_path; do
    [[ -z "$file_path" ]] && continue
    if echo "$file_path" | grep -q -E '(Guard|Helper|Util|Utility)\.cs$'; then
      record_failure "新增潜在同义工具类文件，请确认是否应复用现有工具，违反规则 23：$file_path"
    fi
  done <<< "$added_cs_files"
}

# 检查 Copilot 交流语言约束（基于 PR 文本增量检查）。
check_copilot_language_rule() {
  log_step "执行规则校验：Copilot 交流中文约束"
  if ! is_pull_request_context; then
    log_step "当前不是 pull_request 上下文，跳过规则 13 的上下文校验"
    return
  fi

  local event_path="${GITHUB_EVENT_PATH:-}"
  if [[ -z "$event_path" || ! -f "$event_path" ]]; then
    log_step "未提供 GITHUB_EVENT_PATH，跳过规则 13 的事件文本检查"
    return
  fi

  local pr_title
  pr_title="$(grep -o -E '"title":[[:space:]]*"[^"]*"' "$event_path" | head -n 1 || true)"
  local actor
  actor="${GITHUB_ACTOR:-}"
  if [[ ( "$actor" == "github-copilot[bot]" || "$actor" == "copilot-autofix[bot]" ) && -n "$pr_title" ]] && ! contains_chinese_char "$pr_title"; then
    record_failure "PR 标题缺少中文语义，违反规则 13。"
  fi
}

# 检查 Copilot 默认创建 PR 约束（分支命名约定）。
check_copilot_pr_creation_rule() {
  log_step "执行规则校验：Copilot 任务默认由 Copilot 创建 PR"
  if ! is_pull_request_context; then
    log_step "当前不是 pull_request 上下文，跳过规则 16 的分支约束检查"
    return
  fi

  local head_ref="${GITHUB_HEAD_REF:-}"
  if [[ -z "$head_ref" ]]; then
    return
  fi

  local actor="${GITHUB_ACTOR:-}"
  if [[ "$actor" != "github-copilot[bot]" && "$actor" != "copilot-autofix[bot]" ]]; then
    log_step "当前 PR 非 Copilot bot 创建，跳过规则 16 的分支前缀检查"
    return
  fi

  if [[ ! "$head_ref" =~ ^copilot/ ]]; then
    record_failure "PR 源分支未使用 copilot/ 前缀，违反规则 16：${head_ref}"
  fi
}

# 检查影分身代码修复要求（复用重复签名检查）。
check_shadow_duplicate_code_rule() {
  log_step "执行规则校验：影分身代码检查"
  check_no_duplicate_method_signature
}

# 检查层级边界约束（项目引用方向）。
check_layer_boundary_rule() {
  log_step "执行规则校验：结构层级边界"

  local domain_ref
  domain_ref="$(grep -n -E 'ProjectReference.+(Host|Infrastructure)\.csproj' Zeye.Sorting.Hub.Domain/Zeye.Sorting.Hub.Domain.csproj || true)"
  if [[ -n "$domain_ref" ]]; then
    record_failure "Domain 项目存在越层引用，违反规则 18：\n$domain_ref"
  fi

  local app_ref
  app_ref="$(grep -n -E 'ProjectReference.+Zeye\.Sorting\.Hub\.Host\.csproj' Zeye.Sorting.Hub.Application/Zeye.Sorting.Hub.Application.csproj || true)"
  if [[ -n "$app_ref" ]]; then
    record_failure "Application 项目引用 Host，违反规则 18：\n$app_ref"
  fi
}

# 检查性能敏感反模式（增量）。
check_performance_patterns() {
  log_step "执行规则校验：性能敏感反模式"
  if ! is_pull_request_context; then
    log_step "当前不是 pull_request 上下文，跳过规则 19 的增量校验"
    return
  fi

  local changed_cs_files
  changed_cs_files="$(get_pr_changed_files_by_suffix "cs")"
  if [[ -z "$changed_cs_files" ]]; then
    return
  fi

  local pattern='ToList\(\)\.Count|Any\(\)[[:space:]]*==[[:space:]]*true'
  local file_path=""
  while IFS= read -r file_path; do
    [[ -z "$file_path" ]] && continue
    [[ -f "$file_path" ]] || continue
    local hit
    hit="$(grep -n -E "$pattern" "$file_path" || true)"
    if [[ -n "$hit" ]]; then
      record_failure "检测到性能反模式，违反规则 19：\n${file_path}:\n${hit}"
    fi
  done <<< "$changed_cs_files"
}

# 检查命名规范（新增 C# 文件必须 PascalCase）。
check_naming_conventions() {
  log_step "执行规则校验：命名规范"
  if ! is_pull_request_context; then
    log_step "当前不是 pull_request 上下文，跳过规则 21 的增量校验"
    return
  fi

  local added_cs_files
  added_cs_files="$(get_pr_added_files_by_suffix "cs")"
  if [[ -z "$added_cs_files" ]]; then
    return
  fi

  local file_path=""
  while IFS= read -r file_path; do
    [[ -z "$file_path" ]] && continue
    local file_name
    file_name="$(basename "$file_path" .cs)"
    if ! echo "$file_name" | grep -q -E '^[A-Z][A-Za-z0-9_]*$'; then
      record_failure "新增 C# 文件未使用 PascalCase 命名，违反规则 21：$file_path"
    fi
  done <<< "$added_cs_files"
}

# 检查 Swagger 相关中文注释（增量）。
check_swagger_chinese_comments() {
  log_step "执行规则校验：Swagger 参数/方法/枚举中文注释"
  if ! is_pull_request_context; then
    log_step "当前不是 pull_request 上下文，跳过规则 24 的增量校验"
    return
  fi

  ensure_pr_diff_ready
  local changed_swagger_files
  changed_swagger_files="$(echo "$PR_DIFF_NAME_ONLY" | grep -E "$SWAGGER_RELATED_PATH_PATTERN" || true)"
  if [[ -z "$changed_swagger_files" ]]; then
    return
  fi

  local file_path=""
  while IFS= read -r file_path; do
    [[ -z "$file_path" ]] && continue
    [[ -f "$file_path" ]] || continue
    local added_comment_lines
    added_comment_lines="$(git --no-pager diff --unified=0 "origin/${GITHUB_BASE_REF}...HEAD" -- "$file_path" | grep -E '^\+' | grep -v -E '^\+\+\+' | grep -E '^[+][[:space:]]*///' || true)"
    if [[ -n "$added_comment_lines" ]] && ! echo "$added_comment_lines" | grep -q -P '\p{Han}'; then
      record_failure "Swagger 相关新增/修改注释未包含中文，违反规则 24：$file_path"
    fi
  done <<< "$changed_swagger_files"
}

# 检查每个类独立文件（全量）。
check_single_class_per_file() {
  log_step "执行规则校验：每个类独立文件"

  local cs_files=""
  if is_pull_request_context; then
    cs_files="$(get_pr_changed_files_by_suffix "cs")"
  else
    cs_files="$(find . -type f -name '*.cs' ! -path './.git/*' ! -path '*/bin/*' ! -path '*/obj/*')"
  fi

  if [[ -z "$cs_files" ]]; then
    return
  fi

  local result
  result="$(echo "$cs_files" | while IFS= read -r file_path; do
    [[ -f "$file_path" ]] || continue
    local class_count
    class_count="$(awk '
      BEGIN { in_block = 0; count = 0 }
      {
        line = $0
        if (in_block == 1) {
          if (line ~ /\*\//) { in_block = 0 }
          next
        }
        if (line ~ /^[[:space:]]*\/\//) { next }
        if (line ~ /\/\*/) {
          if (line !~ /\*\//) { in_block = 1 }
          next
        }
        if (line ~ /^[[:space:]]*(public|internal|private|protected)?[[:space:]]*(sealed[[:space:]]+|abstract[[:space:]]+|static[[:space:]]+|partial[[:space:]]+)*class[[:space:]]+/) {
          count++
        }
      }
      END { print count }
    ' "$file_path")"
    if [[ "$class_count" -gt 1 ]]; then
      echo "${file_path}: ${class_count}"
    fi
  done)"
  if [[ -n "$result" ]]; then
    record_failure "检测到单文件多个类，违反规则 25：\n$result"
  fi
}

# 检查 Markdown 文件命名规范。
check_md_chinese_naming() {
  log_step "执行规则校验：README 外 md 文件中文命名"

  if ! is_pull_request_context; then
    log_step "当前不是 pull_request 上下文，跳过规则 26 的增量校验"
    return
  fi

  local changed_md_files
  changed_md_files="$(get_pr_changed_files_by_suffix "md")"
  if [[ -z "$changed_md_files" ]]; then
    return
  fi

  local md_files
  md_files="$(echo "$changed_md_files")"
  local invalid=""
  local file_path=""
  while IFS= read -r file_path; do
    [[ -z "$file_path" ]] && continue
    local file_name
    file_name="$(basename "$file_path")"
    if [[ "$file_name" == "README.md" || "$file_path" == ".github/copilot-instructions.md" ]]; then
      continue
    fi
    if ! contains_chinese_char "$file_name"; then
      invalid+="$file_path"$'\n'
    fi
  done <<< "$md_files"

  if [[ -n "$invalid" ]]; then
    record_failure "检测到非中文命名的 md 文件，违反规则 26：\n$invalid"
  fi
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

  if [[ "$normalized_text" == *"doc/pdf文档解析到md"* ]]; then
    check_doc_pdf_to_md_traceability
    return
  fi

  if [[ "$normalized_text" == *"方法都需要有注释"* ]]; then
    check_method_comments
    return
  fi

  if [[ "$normalized_text" == *"全局禁止代码重复"* ]]; then
    check_no_duplicate_method_signature
    return
  fi

  if [[ "$normalized_text" == *"小工具类尽量代码简洁"* ]]; then
    check_tool_class_quality
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

  if [[ "$normalized_text" == *"所有的异常都必须输出日志"* ]]; then
    check_exception_logging
    return
  fi

  if [[ "$normalized_text" == *"Copilot的回答/描述/交流都需要使用中文"* ]]; then
    check_copilot_language_rule
    return
  fi

  if [[ "$normalized_text" == *"所有的类的字段都必须有注释"* ]]; then
    check_field_comments
    return
  fi

  if [[ "$normalized_text" == *"日志只能使用Nlog"* ]]; then
    check_nlog_only
    return
  fi

  if [[ "$normalized_text" == *"Copilot任务默认由Copilot创建拉取请求"* ]]; then
    check_copilot_pr_creation_rule
    return
  fi

  if [[ "$normalized_text" == *"每次修改代码后都需要检查是否影分身代码"* ]]; then
    check_shadow_duplicate_code_rule
    return
  fi

  if [[ "$normalized_text" == *"严格划分结构层级边界"* ]]; then
    check_layer_boundary_rule
    return
  fi

  if [[ "$normalized_text" == *"性能更高的特性标记"* ]]; then
    check_performance_patterns
    return
  fi

  if [[ "$normalized_text" == *"注释中禁止出现第二人称"* ]]; then
    check_no_second_person_in_comments
    return
  fi

  if [[ "$normalized_text" == *"命名有严格要求"* ]]; then
    check_naming_conventions
    return
  fi

  if [[ "$normalized_text" == *"历史更新记录不要写在README.md"* ]]; then
    check_no_history_in_readme
    return
  fi

  if [[ "$normalized_text" == *"工具代码需要提取集中"* ]]; then
    check_tool_code_centralization
    return
  fi

  if [[ "$normalized_text" == *"swagger的所有参数、方法、枚举项都必须要有中文注释"* ]]; then
    check_swagger_chinese_comments
    return
  fi

  if [[ "$normalized_text" == *"每个类都需要独立的文件"* ]]; then
    check_single_class_per_file
    return
  fi

  if [[ "$normalized_text" == *"md文件除README.md外"* ]]; then
    check_md_chinese_naming
    return
  fi

  if [[ "$normalized_text" == *"禁止使用过时标记"* ]]; then
    check_no_obsolete_attribute
    return
  fi

  if [[ "$normalized_text" == *"命名空间必须与物理目录层级严格一致"* ]]; then
    check_namespace_matches_directory
    return
  fi

  if [[ "$normalized_text" == *"禁止在热路径读写配置文件"* || "$normalized_text" == *"禁止在热路径"* ]]; then
    check_no_hot_path_config_db_access
    return
  fi

  if [[ "$normalized_text" == *"配置项的注释都需要写明"* || "$normalized_text" == *"枚举类型需要列出所有枚举项"* ]]; then
    check_config_comment_range_annotation
    return
  fi

  record_failure "发现未映射的 Copilot 规则，请同步更新 CI 校验逻辑（规则 ${rule_number}: ${rule_text}）。"
}

# 检查热路径内是否有直接的配置文件读写或 DbContext 操作（增量校验）。
# 热路径识别：每次请求必经的控制器 Action / 中间件 / 后台循环体中。
check_no_hot_path_config_db_access() {
  log_step "执行规则校验：禁止在热路径读写配置文件和数据库"

  if ! is_pull_request_context; then
    log_step "当前不是 pull_request 上下文，跳过规则 29 的增量校验"
    return
  fi

  local changed_cs_files
  changed_cs_files="$(get_pr_changed_files_by_suffix "cs")"
  if [[ -z "$changed_cs_files" ]]; then
    return
  fi

  # 热路径定义：仅检查 Controllers/ 和 Middleware/ 目录下的文件（请求处理主链路）。
  # 排除 DI 扩展（*Extensions.cs）、HostedService（*HostedService.cs）、
  # 设计时工厂（DesignTime/）、Program.cs 等启动期代码。
  local violations=""
  local file_path=""
  while IFS= read -r file_path; do
    [[ -z "$file_path" ]] && continue
    [[ ! -f "$file_path" ]] && continue
    # 仅扫描 Controllers/ 或 Middleware/ 路径下的文件
    if [[ "$file_path" != *"/Controllers/"* && "$file_path" != *"/Middleware/"* ]]; then
      continue
    fi
    # 跳过扩展类与中间件扩展注册文件（这些属于 DI 注册启动期代码）
    local file_name
    file_name="$(basename "$file_path")"
    if [[ "$file_name" == *Extensions.cs ]]; then
      continue
    fi
    # 检测在中间件 Invoke/InvokeAsync 或 Controller Action 中直接读取配置索引器或文件 IO
    local hit
    hit="$(grep -nE '(configuration\[|_configuration\[|IConfiguration.*GetSection|IConfiguration.*GetValue|File\.ReadAll)' "$file_path" 2>/dev/null || true)"
    if [[ -n "$hit" ]]; then
      violations+="$file_path:\n$hit\n"
    fi
  done <<< "$changed_cs_files"

  if [[ -n "$violations" ]]; then
    record_failure "检测到热路径（Controller/Middleware）中疑似读写配置文件，违反规则 29：\n$violations"
  fi
}

# 检查配置项字段注释是否包含可填写范围说明（增量校验）。
check_config_comment_range_annotation() {
  log_step "执行规则校验：配置项注释范围说明"

  if ! is_pull_request_context; then
    log_step "当前不是 pull_request 上下文，跳过规则 30 的增量校验"
    return
  fi

  local changed_cs_files
  changed_cs_files="$(get_pr_changed_files_by_suffix "cs")"
  if [[ -z "$changed_cs_files" ]]; then
    return
  fi

  # 检测以 Options / Config / Configuration 结尾的 POCO 类文件中
  # 是否存在无注释的公共属性（缺少 /// <summary> 或 // 注释）
  local violations=""
  local file_path=""
  while IFS= read -r file_path; do
    [[ -z "$file_path" ]] && continue
    [[ ! -f "$file_path" ]] && continue
    # 只检查 Options/Config/Configuration 结尾的配置类文件
    local file_name
    file_name="$(basename "$file_path" .cs)"
    if [[ "$file_name" != *Options && "$file_name" != *Config && "$file_name" != *Configuration ]]; then
      continue
    fi
    # 检查 public 属性前是否有 summary 注释
    local prev_line=""
    while IFS= read -r line; do
      if [[ "$line" =~ ^[[:space:]]*(public)[[:space:]]+(.*)[[:space:]]+(get|set|init|\{) ]]; then
        if [[ "$prev_line" != *"///"* && "$prev_line" != *"//"* ]]; then
          violations+="$file_path: 公共属性缺少注释（范围说明）: $line\n"
        fi
      fi
      prev_line="$line"
    done < "$file_path"
  done <<< "$changed_cs_files"

  if [[ -n "$violations" ]]; then
    record_failure "检测到配置类公共属性缺少注释（需说明可填写范围），违反规则 30：\n$violations"
  fi
}

# 检查命名空间与目录层级一致（增量）。
check_namespace_matches_directory() {
  log_step "执行规则校验：命名空间与目录层级一致"
  if ! is_pull_request_context; then
    log_step "当前不是 pull_request 上下文，跳过规则 28 的增量校验"
    return
  fi

  local changed_cs_files
  changed_cs_files="$(get_pr_changed_files_by_suffix "cs")"
  if [[ -z "$changed_cs_files" ]]; then
    return
  fi

  local file_path=""
  while IFS= read -r file_path; do
    [[ -z "$file_path" ]] && continue
    [[ -f "$file_path" ]] || continue
    if [[ "$file_path" =~ \.Tests/ ]]; then
      continue
    fi

    local namespace_line
    namespace_line="$(grep -n -E '^[[:space:]]*namespace[[:space:]]+[A-Za-z0-9_\.]+[[:space:]]*;?[[:space:]]*$' "$file_path" | head -n 1 || true)"
    [[ -z "$namespace_line" ]] && continue
    local declared_namespace
    declared_namespace="$(echo "$namespace_line" | sed -E 's/^[0-9]+:[[:space:]]*namespace[[:space:]]+([A-Za-z0-9_\.]+).*/\1/')"

    local expected_namespace
    if [[ "$file_path" =~ ^Zeye\.Sorting\.Hub\..*\.cs$ ]]; then
      expected_namespace="$(echo "$file_path" | sed -E 's/\.cs$//; s#/[^/]+$##; s#/#.#g')"
      if [[ "$declared_namespace" != "$expected_namespace" ]]; then
        record_failure "命名空间与目录层级不一致，违反规则 28：${file_path}（声明：${declared_namespace}，期望：${expected_namespace}）"
      fi
    fi
  done <<< "$changed_cs_files"
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
