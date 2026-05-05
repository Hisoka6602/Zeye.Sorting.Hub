using Microsoft.Extensions.Configuration;
using Zeye.Sorting.Hub.Application.Abstractions.Diagnostics;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.QueryGovernance;

/// <summary>
/// 查询治理索引建议服务。
/// </summary>
public sealed class QueryIndexRecommendationService {
    /// <summary>
    /// 慢查询画像读取器。
    /// </summary>
    private readonly ISlowQueryProfileReader _slowQueryProfileReader;

    /// <summary>
    /// 查询模板注册表。
    /// </summary>
    private readonly QueryTemplateRegistry _queryTemplateRegistry;

    /// <summary>
    /// 触发索引建议的最小 P99 阈值（毫秒）。
    /// </summary>
    private readonly int _recommendationP99Milliseconds;

    /// <summary>
    /// 触发索引建议的最小调用次数。
    /// </summary>
    private readonly int _minimumCallCount;

    /// <summary>
    /// 浮点毫秒值相等比较容差。
    /// </summary>
    private const double MillisecondComparisonTolerance = 0.01d;

    /// <summary>
    /// 初始化查询治理索引建议服务。
    /// </summary>
    /// <param name="slowQueryProfileReader">慢查询画像读取器。</param>
    /// <param name="queryTemplateRegistry">查询模板注册表。</param>
    /// <param name="configuration">配置根。</param>
    public QueryIndexRecommendationService(
        ISlowQueryProfileReader slowQueryProfileReader,
        QueryTemplateRegistry queryTemplateRegistry,
        IConfiguration configuration) {
        ArgumentNullException.ThrowIfNull(configuration);
        _slowQueryProfileReader = slowQueryProfileReader ?? throw new ArgumentNullException(nameof(slowQueryProfileReader));
        _queryTemplateRegistry = queryTemplateRegistry ?? throw new ArgumentNullException(nameof(queryTemplateRegistry));
        _recommendationP99Milliseconds = AutoTuningConfigurationReader.GetPositiveIntOrDefault(
            configuration,
            AutoTuningConfigurationReader.BuildAutoTuningKey("QueryGovernance:RecommendationP99Milliseconds"),
            AutoTuningConfigurationReader.GetPositiveIntOrDefault(
                configuration,
                AutoTuningConfigurationReader.BuildAutoTuningKey("SlowQueryThresholdMilliseconds"),
                500));
        _minimumCallCount = AutoTuningConfigurationReader.GetPositiveIntOrDefault(
            configuration,
            AutoTuningConfigurationReader.BuildAutoTuningKey("QueryGovernance:MinimumCallCount"),
            3);
    }

    /// <summary>
    /// 构建查询治理报告。
    /// </summary>
    /// <returns>查询治理报告。</returns>
    public QueryGovernanceReport BuildReport() {
        var registeredTemplates = _queryTemplateRegistry.GetAll();
        var (profiles, totalFingerprintCount) = _slowQueryProfileReader.GetTopProfiles();
        var matchedProfilesByTemplateName = new Dictionary<string, SlowQueryProfileReadModel>(StringComparer.OrdinalIgnoreCase);
        var matchedFingerprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in profiles) {
            var bestTemplate = FindBestTemplate(profile, registeredTemplates);
            if (bestTemplate is null) {
                continue;
            }

            matchedFingerprints.Add(profile.Fingerprint);
            if (!matchedProfilesByTemplateName.TryGetValue(bestTemplate.TemplateName, out var currentProfile)
                || profile.P99Milliseconds > currentProfile.P99Milliseconds
                || (Math.Abs(profile.P99Milliseconds - currentProfile.P99Milliseconds) < MillisecondComparisonTolerance && profile.CallCount > currentProfile.CallCount)) {
                matchedProfilesByTemplateName[bestTemplate.TemplateName] = profile;
            }
        }

        var matchedTemplateCount = matchedProfilesByTemplateName.Count;
        var templatesWithoutObservedProfiles = registeredTemplates
            .Where(template => !matchedProfilesByTemplateName.ContainsKey(template.TemplateName))
            .Select(static template => template.TemplateName)
            .ToArray();
        var recommendations = registeredTemplates
            .Where(template => matchedProfilesByTemplateName.TryGetValue(template.TemplateName, out _))
            .Select(template => (Template: template, Profile: matchedProfilesByTemplateName[template.TemplateName]))
            .Where(static item => item.Profile is not null)
            .Where(item => ShouldEmitRecommendation(item.Profile))
            .Select(item => BuildRecommendation(item.Template, item.Profile))
            .ToArray();

        var unmatchedFingerprints = profiles
            .Where(profile => !matchedFingerprints.Contains(profile.Fingerprint))
            .Select(static profile => profile.Fingerprint)
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();

        return new QueryGovernanceReport {
            GeneratedAtLocal = DateTime.Now,
            RegisteredTemplateCount = registeredTemplates.Count,
            ObservedSlowQueryFingerprintCount = totalFingerprintCount,
            MatchedTemplateCount = matchedTemplateCount,
            TemplatesWithoutObservedProfiles = templatesWithoutObservedProfiles,
            UnmatchedSlowQueryFingerprints = unmatchedFingerprints,
            RegisteredTemplates = registeredTemplates,
            Recommendations = recommendations
                .OrderByDescending(static item => item.ObservedP99Milliseconds)
                .ThenByDescending(static item => item.ObservedCallCount)
                .ThenBy(static item => item.TemplateName, StringComparer.Ordinal)
                .ToArray()
        };
    }

    /// <summary>
    /// 判断画像是否需要输出索引建议。
    /// </summary>
    /// <param name="profile">画像读模型。</param>
    /// <returns>是否输出建议。</returns>
    private bool ShouldEmitRecommendation(SlowQueryProfileReadModel profile) {
        return profile.P99Milliseconds >= _recommendationP99Milliseconds
            && profile.CallCount >= _minimumCallCount;
    }

    /// <summary>
    /// 为慢查询画像寻找最佳模板。
    /// </summary>
    /// <param name="profile">慢查询画像。</param>
    /// <param name="templates">模板集合。</param>
    /// <returns>最佳模板；若未命中则返回 null。</returns>
    private static QueryTemplateDescriptor? FindBestTemplate(
        SlowQueryProfileReadModel profile,
        IReadOnlyList<QueryTemplateDescriptor> templates) {
        QueryTemplateDescriptor? bestTemplate = null;
        var bestScore = 0;
        foreach (var template in templates) {
            var score = CalculateMatchScore(template, profile.NormalizedSql);
            if (score <= 0) {
                continue;
            }

            if (bestTemplate is null || IsBetterTemplateMatch(template, bestTemplate, score, bestScore)) {
                bestTemplate = template;
                bestScore = score;
            }
        }

        return bestTemplate;
    }

    /// <summary>
    /// 判断候选模板是否优于当前模板。
    /// </summary>
    /// <param name="candidateTemplate">候选模板。</param>
    /// <param name="currentTemplate">当前最佳模板。</param>
    /// <param name="candidateScore">候选模板得分。</param>
    /// <param name="currentScore">当前模板得分。</param>
    /// <returns>若候选模板应替换当前模板则返回 true。</returns>
    private static bool IsBetterTemplateMatch(
        QueryTemplateDescriptor candidateTemplate,
        QueryTemplateDescriptor currentTemplate,
        int candidateScore,
        int currentScore) {
        // 步骤 1：优先选择匹配分值更高的模板，确保表名/过滤/排序字段覆盖更完整。
        if (candidateScore != currentScore) {
            return candidateScore > currentScore;
        }

        // 步骤 2：分值相同时优先选择过滤字段更少的模板，避免通用模板吞掉更明确的专用模板。
        if (candidateTemplate.FilterColumns.Count != currentTemplate.FilterColumns.Count) {
            return candidateTemplate.FilterColumns.Count < currentTemplate.FilterColumns.Count;
        }

        // 步骤 3：若仍然相同，则按模板名称字典序稳定收敛，保证输出结果可预测。
        return candidateTemplate.TemplateName.CompareTo(currentTemplate.TemplateName) < 0;
    }

    /// <summary>
    /// 计算模板与 SQL 的匹配分值。
    /// </summary>
    /// <param name="template">模板描述。</param>
    /// <param name="normalizedSql">归一化 SQL。</param>
    /// <returns>匹配分值。</returns>
    private static int CalculateMatchScore(QueryTemplateDescriptor template, string normalizedSql) {
        if (string.IsNullOrWhiteSpace(normalizedSql)) {
            return 0;
        }

        var tableMatchCount = CountMatchedTokens(template.TableNames, normalizedSql);
        if (tableMatchCount == 0) {
            return 0;
        }

        var filterMatchCount = CountMatchedTokens(template.FilterColumns, normalizedSql);
        var sortMatchCount = CountMatchedTokens(template.SortColumns, normalizedSql);
        if (filterMatchCount == 0 && sortMatchCount == 0) {
            return 0;
        }

        return tableMatchCount * 100 + filterMatchCount * 10 + sortMatchCount;
    }

    /// <summary>
    /// 统计 SQL 中命中的字段标记数量。
    /// </summary>
    /// <param name="tokens">字段或表名标记集合。</param>
    /// <param name="normalizedSql">归一化 SQL。</param>
    /// <returns>命中数量。</returns>
    private static int CountMatchedTokens(IReadOnlyList<string> tokens, string normalizedSql) {
        var matchCount = 0;
        foreach (var token in tokens) {
            if (string.IsNullOrWhiteSpace(token)) {
                continue;
            }

            if (normalizedSql.Contains(token, StringComparison.OrdinalIgnoreCase)) {
                matchCount++;
            }
        }

        return matchCount;
    }

    /// <summary>
    /// 构建索引建议。
    /// </summary>
    /// <param name="template">模板描述。</param>
    /// <param name="profile">慢查询画像。</param>
    /// <returns>索引建议。</returns>
    private static QueryIndexRecommendation BuildRecommendation(
        QueryTemplateDescriptor template,
        SlowQueryProfileReadModel profile) {
        var recommendedIndex = template.RecommendedIndexes.Count > 0
            ? template.RecommendedIndexes[0]
            : $"{template.TableNames[0]}({string.Join(", ", template.FilterColumns.Concat(template.SortColumns).Distinct(StringComparer.OrdinalIgnoreCase))})";
        var confidence = CalculateConfidence(template, profile);
        return new QueryIndexRecommendation {
            TemplateName = template.TemplateName,
            Fingerprint = profile.Fingerprint,
            TableName = template.TableNames[0],
            RecommendedIndex = recommendedIndex,
            Reason = $"模板 {template.TemplateName} 在当前窗口内出现慢查询，P99={profile.P99Milliseconds:F1}ms，调用次数={profile.CallCount}，建议优先核查声明索引 {recommendedIndex}。",
            RiskLevel = profile.TimeoutCount > 0 || profile.ErrorCount > 0 ? "高" : profile.P99Milliseconds >= 1000d ? "中" : "低",
            Confidence = confidence,
            ObservedP99Milliseconds = profile.P99Milliseconds,
            ObservedCallCount = profile.CallCount,
            NormalizedSql = profile.NormalizedSql,
            SampleSql = profile.SampleSql
        };
    }

    /// <summary>
    /// 计算建议置信度。
    /// </summary>
    /// <param name="template">模板描述。</param>
    /// <param name="profile">慢查询画像。</param>
    /// <returns>置信度。</returns>
    private static decimal CalculateConfidence(QueryTemplateDescriptor template, SlowQueryProfileReadModel profile) {
        var coverage = CalculateMatchScore(template, profile.NormalizedSql);
        var callCountFactor = Math.Min(profile.CallCount, 20) / 20m;
        var latencyFactor = Math.Min((decimal)profile.P99Milliseconds, 2000m) / 2000m;
        return decimal.Clamp(0.35m + coverage / 200m + callCountFactor * 0.25m + latencyFactor * 0.15m, 0m, 0.99m);
    }
}
