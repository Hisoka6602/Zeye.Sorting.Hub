import { Link } from 'react-router';
import { SectionCard } from '../../components/SectionCard';
import type { ParcelComparisonItem } from '../../data/api/parcelComparisonTypes';
import { comparisonDelta, comparisonMeasures, comparisonValue, formatComparisonValue, type ComparisonMeasure } from './parcelComparisonModel';

export const comparisonColors = ['#246bce', '#077b86', '#7540b4', '#b26a0d', '#397e29', '#b64173', '#496480', '#947130'];
export const comparisonLabel = (id: string, ids: string[]) => 'P' + (ids.indexOf(id) + 1);
export const comparisonColor = (id: string, ids: string[]) => comparisonColors[Math.max(0, ids.indexOf(id))];

/** 明确呈现绝对差值及其单位，缺少基准时保留空值。 */
export function ComparisonDifference({ value, baseline, unit, isBaseline }: { value: number | null; baseline: number | null; unit: string; isBaseline?: boolean }) {
  const delta = comparisonDelta(value, baseline).difference;
  return <small className="parcel-comparison-difference">{isBaseline ? '对比基准' : delta === null ? '差值 —' : `Δ ${delta > 0 ? '+' : ''}${formatComparisonValue(delta)} ${unit}`}</small>;
}

function MeasurementBars({ items, ids, baseline, measure, title, unit }: {
  items: ParcelComparisonItem[]; ids: string[]; baseline: ParcelComparisonItem; measure: ComparisonMeasure; title: string; unit: string;
}) {
  const values = items.map(item => comparisonValue(item, measure));
  const max = Math.max(0, ...values.filter((value): value is number => value !== null));
  const base = comparisonValue(baseline, measure);
  return <SectionCard title={title} extra={<span className="text-muted">{unit}</span>} className="parcel-comparison-bars-card">
    <div className="parcel-comparison-bars" role="group" aria-label={title + '柱状对比'}>
      {items.map((item, index) => {
        const id = item.timing.id, value = values[index], label = comparisonLabel(id, ids);
        return <div className="parcel-comparison-bar-row" key={id} aria-label={`${label}，${item.timing.barCodes || id}，${title}${value === null ? '未提供有效值' : formatComparisonValue(value) + ' ' + unit}`}>
          <span className="parcel-comparison-bar-label">{label}{id === baseline.timing.id && <small>基准</small>}</span>
          <div className="parcel-comparison-bar-track" aria-hidden>{value !== null && <i style={{ width: max > 0 ? value / max * 100 + '%' : '0%', background: comparisonColor(id, ids) }} />}</div>
          <span className="parcel-comparison-bar-value"><b>{value === null ? '—' : formatComparisonValue(value)} <em>{value === null ? '' : unit}</em></b>
            <ComparisonDifference value={value} baseline={base} unit={unit} isBaseline={id === baseline.timing.id} /></span>
        </div>;
      })}
    </div>
    <p className="parcel-comparison-note">{values.every(value => value === null) ? '所选包裹均未提供有效的' + title + '。' : '从 0 起的共同刻度，长度可直接比较。'}{base === null && ' 基准未提供有效值，差值不可计算。'}</p>
  </SectionCard>;
}

/** 量测图用于看差距，交叉表保留每票尺寸及精确值；体积只换算已保存的mm³。 */
export function ParcelComparisonMeasurements({ items, ids, baseline }: { items: ParcelComparisonItem[]; ids: string[]; baseline: ParcelComparisonItem }) {
  return <>
    <div className="parcel-comparison-charts">
      <MeasurementBars items={items} ids={ids} baseline={baseline} measure="weight" title="重量对比" unit="kg" />
      <MeasurementBars items={items} ids={ids} baseline={baseline} measure="volume" title="体积对比" unit="L" />
    </div>
    <SectionCard title="量测与格口明细" extra={<span className="text-muted">Δ 相对 {comparisonLabel(baseline.timing.id, ids)}</span>}>
      <div className="parcel-comparison-table-scroll" tabIndex={0} role="region" aria-label="包裹量测对比表">
        <table className="parcel-comparison-table">
          <caption>当前已保存快照：重量 kg，尺寸 mm，体积 L；差值为当前包裹减去基准包裹。</caption>
          <thead><tr><th scope="col">指标</th>{items.map(item => <th scope="col" key={item.timing.id} className={item === baseline ? 'is-baseline' : ''}>
            <span className="parcel-comparison-table-label" style={{ color: comparisonColor(item.timing.id, ids) }}>{comparisonLabel(item.timing.id, ids)}{item === baseline && ' · 基准'}</span>
            <Link to={'/parcels/' + item.timing.id} title={item.timing.barCodes || item.timing.id}>{item.timing.barCodes || '未提供条码'}</Link>
            <small>ID {item.timing.id}</small>
            <small title={[item.timing.workstationName, item.timing.sourceInstanceId].filter(Boolean).join(' / ')}>{item.timing.workstationName || '未提供工作台'}</small>
          </th>)}</tr></thead>
          <tbody>{comparisonMeasures.map(measure => <tr key={measure.key}>
            <th scope="row">{measure.label}<small>{measure.unit}</small></th>{items.map(item => {
              const value = comparisonValue(item, measure.key);
              return <td key={item.timing.id} className={item === baseline ? 'is-baseline' : ''}><b>{value === null ? item[measure.key] == null ? '未提供' : '无效值' : formatComparisonValue(value)}</b>
                <ComparisonDifference value={value} baseline={comparisonValue(baseline, measure.key)} unit={measure.unit} isBaseline={item === baseline} /></td>;
            })}
          </tr>)}
          {(['targetChuteCode', 'actualChuteCode'] as const).map(key => <tr key={key}><th scope="row">{key === 'targetChuteCode' ? '目标格口' : '实际格口'}</th>{items.map(item => <td key={item.timing.id} className={item === baseline ? 'is-baseline' : ''}>{item[key] ?? '未提供'}</td>)}</tr>)}
          </tbody>
        </table>
      </div>
      <p className="parcel-comparison-note">量测使用包裹当前快照。1 L = 1,000,000 mm³；缺少体积时不以长 × 宽 × 高代替。缺失或无效量测不参与差值计算。</p>
    </SectionCard>
  </>;
}
