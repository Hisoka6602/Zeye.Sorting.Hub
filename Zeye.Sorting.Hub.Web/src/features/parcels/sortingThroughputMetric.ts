import { formatNumber } from '../../data/formatNumber.ts';
interface CreationIntervals {
  medianCreationIntervalMilliseconds: number | null;
  minimumCreationIntervalMilliseconds: number | null;
  actualSortingThroughputPerHour: number | null;
  theoreticalSortingThroughputPerHour: number | null;
  creationIntervalSampleCount: number;
}

/** 实际时效采用中位间隔，理论时效采用最短正间隔；两者都独立于完成耗时。 */
export function sortingThroughputMetric(data: CreationIntervals | null | undefined) {
  const samples = data?.creationIntervalSampleCount ?? 0;
  const positiveNumber = (value: number) => value < 0.01 ? '< 0.01' : formatNumber(value, { grouping: true });
  const metric = (interval: number | null | undefined, rate: number | null | undefined, description: string) => {
    const available = interval != null && interval > 0 && Number.isFinite(interval)
      && rate != null && rate > 0 && Number.isFinite(rate) && samples > 0;
    return {
      value: available ? positiveNumber(rate) : '—',
      note: available ? description : '暂无有效时效数据',
    };
  };
  return {
    actual: metric(data?.medianCreationIntervalMilliseconds, data?.actualSortingThroughputPerHour, '当前处理节奏下的小时产能'),
    theoretical: metric(data?.minimumCreationIntervalMilliseconds, data?.theoreticalSortingThroughputPerHour, '最佳处理节奏下的预估产能'),
    hint: '实际时效反映所选时间范围内的常态处理效率，理论时效反映最佳处理节奏下的预估产能。两项指标可用于评估当前分拣表现。',
  };
}
