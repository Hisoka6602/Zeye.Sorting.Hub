import { App, Button, Descriptions, InputNumber, Space, Switch, Typography } from 'antd';
import { useState } from 'react';
import { NumberUnit } from '../../components/NumberUnit';
import { PageIntro } from '../../components/PageIntro';
import { SectionCard } from '../../components/SectionCard';
import { contentTypographyForPath } from '../../app/contentTypography';

type SettingsValues = { enabled: boolean; sample: number; retention: number; redaction: boolean; databasePool: number; diagnostics: boolean };
const initialSettings: SettingsValues = { enabled: true, sample: 100, retention: 180, redaction: true, databasePool: 100, diagnostics: true };
function readSettings(): SettingsValues { try { return JSON.parse(localStorage.getItem('zeye-demo-settings') || 'null') || initialSettings; } catch { return initialSettings; } }
const settingCategories = ['运行', '数据库', '审计', '保留', '诊断'];
export function SettingsPage() {
  const { message } = App.useApp();
  const [category, setCategory] = useState('审计');
  const [values, setValues] = useState<SettingsValues>(readSettings);
  const update = <K extends keyof SettingsValues>(key: K, value: SettingsValues[K]) => setValues(current => ({ ...current, [key]: value }));
  const save = () => { localStorage.setItem('zeye-demo-settings', JSON.stringify(values)); message.success('配置草稿已保存到本地演示数据'); };
  const reset = () => setValues(readSettings());
  return <>
    <PageIntro title="系统配置" planned description="配置系统的运行行为与安全策略。修改后需按规则生效，请根据业务需要评估风险。" />
    <div className="setting-layout"><div className="setting-menu">{settingCategories.map(item => <button className={category === item ? 'selected' : ''} key={item} onClick={() => setCategory(item)}>{item}</button>)}</div>
      <SectionCard title={`${category}配置`}>
        <p className="text-muted" style={{ marginTop: -5 }}>{category === '审计' ? '用于控制系统审计日志的采集与脱敏策略。' : `用于管理系统${category}相关参数。`}</p>
        <div className="setting-rows">
          {category === '审计' && <>
            <SettingRow label="审计开关" hint="开启后，系统将按配置采集关键操作的审计日志。" modified="2026-09-20 14:32:18 由 张三"><Switch checked={values.enabled} onChange={value => update('enabled', value)} /></SettingRow>
            <SettingRow label="采样率" hint="设置审计日志的采样比例，范围 1% ~ 100%。" modified="2026-09-18 10:21:06 由 李四"><Space className="settings-number" size={10}><InputNumber min={1} max={100} value={values.sample} onChange={value => update('sample', value || 1)} /><span>%</span></Space></SettingRow>
            <SettingRow label="保留天数" hint="审计日志在系统中保留的天数，超出后将按清理策略处理。" modified="2026-09-12 16:08:33 由 王五"><Space className="settings-number" size={10}><InputNumber min={1} value={values.retention} onChange={value => update('retention', value || 1)} /><span>天</span></Space></SettingRow>
            <SettingRow label="敏感字段脱敏" hint="开启后，审计日志中的敏感字段将进行脱敏处理。" modified="2026-09-15 11:27:41 由 张三"><Switch checked={values.redaction} onChange={value => update('redaction', value)} /></SettingRow>
          </>}
          {category === '运行' && <><SettingRow label="系统运行状态" hint="本地演示开关，不影响后台服务。" modified="2026-09-20 14:32:18 由 张三"><Switch checked={values.enabled} onChange={value => update('enabled', value)} /></SettingRow><SettingRow label="当前环境" hint="演示环境名称。" modified="-">Zeye Sorting Hub 演示环境</SettingRow></>}
          {category === '数据库' && <><SettingRow label="连接池上限" hint="用于展示数据库连接池的规划容量。" modified="2026-09-12 16:08:33 由 王五"><InputNumber min={1} max={1000} value={values.databasePool} onChange={value => update('databasePool', value || 1)} /></SettingRow><SettingRow label="数据提供程序" hint="当前仓库支持的数据库类型。" modified="-">MySQL、SQL Server</SettingRow></>}
          {category === '保留' && <SettingRow label="默认保留天数" hint="超出期限的数据按照业务策略归档或清理。" modified="2026-09-12 16:08:33 由 王五"><NumberUnit min={1} value={values.retention} onChange={value => update('retention', value || 1)} unit="天" style={{ width: 165 }} /></SettingRow>}
          {category === '诊断' && <SettingRow label="诊断采集" hint="控制本地演示中的诊断参数。" modified="2026-09-15 11:27:41 由 张三"><Switch checked={values.diagnostics} onChange={value => update('diagnostics', value)} /></SettingRow>}
        </div>
        <div className="setting-source"><b style={contentTypographyForPath('/settings', 'section', '配置来源与生效方式')}>配置来源与生效方式</b><Descriptions bordered size="small" column={1} items={[{ key: '1', label: '配置来源', children: '系统配置（界面维护）' }, { key: '2', label: '生效方式', children: '保存后写入配置库，由系统按既定的刷新机制加载，通常在数分钟内生效。' }, { key: '3', label: '适用范围', children: '当前环境（Zeye Sorting Hub）' }, { key: '4', label: '变更记录', children: <>可在 <Typography.Link onClick={() => message.info('暂无配置变更记录')}>操作日志</Typography.Link> 中查看配置变更历史。</> }, { key: '5', label: '备注', children: <span className="setting-source-note">本页配置不会立即影响正在运行的任务，具体生效时间以系统刷新为准。请在业务低峰时进行变更，并关注相关日志。</span> }]} /></div>
        <div className="settings-actions" style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 16 }}><Button autoInsertSpace={false} onClick={reset}>取消</Button><Button type="primary" onClick={save}>保存草稿</Button></div>
      </SectionCard>
    </div>
  </>;
}

function SettingRow({ label, hint, modified, children }: { label: string; hint: string; modified: string; children: React.ReactNode }) {
  return <div className="setting-row"><div className="setting-label">{label}</div><div><div>{children}</div><div className="setting-help">{hint}</div><div className="setting-help">上次修改：{modified}</div></div></div>;
}
