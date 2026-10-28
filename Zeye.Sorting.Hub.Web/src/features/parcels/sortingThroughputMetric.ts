import { formatNumber } from '../../data/formatNumber.ts';
interface DetectionIntervals {
  medianCreationIntervalMilliseconds: number | null;
  minimumCreationIntervalMilliseconds: number | null;
  actualSortingThroughputPerHour: number | null;
  theoreticalSortingThroughputPerHour: number | null;
  creationIntervalSampleCount: number;
}

/** 时效采用源端检测间隔；接口保留历史创建间隔字段名以兼容已有客户端。 */
export function sortingThroughputMetric(data: DetectionIntervals | null | undefined) {
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
    hint: '按所选日期内的源端检测时间估算：实际时效反映常态处理效率，理论时效反映最佳处理节奏下的预估产能。',
  };
}
