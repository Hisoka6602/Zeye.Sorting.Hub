namespace Zeye.Sorting.Hub.Domain.Enums {

    /// <summary>自动调优闭环自治阶段模型。</summary>
    public enum AutoTuningClosedLoopStage {
        Monitor,
        Diagnose,
        Execute,
        Verify,
        Rollback
    }
}
