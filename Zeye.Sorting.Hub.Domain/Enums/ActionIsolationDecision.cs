namespace Zeye.Sorting.Hub.Domain.Enums {

    /// <summary>危险动作隔离决策。</summary>
    public enum ActionIsolationDecision {
        Execute,
        BlockedByGuard,
        DryRunOnly
    }
}
