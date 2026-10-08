import { Alert, App, AutoComplete, Button, DatePicker, Empty, Form, Input, Result, Select, Skeleton, Switch, Tag, TimePicker } from 'antd';
import dayjs from 'dayjs';
import 'dayjs/locale/zh-cn';
import pickerLocale from 'antd/es/date-picker/locale/zh_CN';
import { CheckCircleOutlined, DatabaseOutlined, HistoryOutlined, ReloadOutlined, SaveOutlined, SearchOutlined, ThunderboltOutlined, UndoOutlined } from '@ant-design/icons';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router';
import { requestApi, ApiError } from '../../data/api/client';
import type { RuntimeConfigurationSaved, RuntimeConfigurationSnapshot } from '../../data/api/configurationTypes';
import { localDateTimeFormat } from '../../data/api/operationalTypes';
import { SectionCard } from '../../components/SectionCard';
import { ApiFeedback } from '../../components/ApiFeedback';
import { ConfigurationNumberPicker } from '../../components/ConfigurationNumberPicker';
import { configurationCategories, configurationDraft, configurationFields, configurationKeyMatches, configurationPatch, configurationRetentionPolicyRows, configurationStringList, configurationTemporalKind, isValidConfigurationTemporalValue, isHotConfigurationField, retentionPolicyConfigurationKey, retentionPolicyNames, type ConfigurationCategory, type ConfigurationDraft, type ConfigurationField, type RetentionPolicyDraftRow } from './configurationModel';
import { configurationFieldPresentation, configurationGroupLabel } from './configurationFields';

interface EditorState {
  latest?: RuntimeConfigurationSnapshot; base?: RuntimeConfigurationSnapshot; draft: ConfigurationDraft;
  loading: boolean; busy: boolean; error?: Error; saved?: RuntimeConfigurationSaved['result'];
}
function hasDraft(state: EditorState): boolean {
  if (!state.base) return false;
  const original = configurationDraft(state.base.configuration);
  return Object.entries(state.draft).some(([key, value]) => value !== original[key]);
}

/** 保留对象使用固定选项，数组整体提交时保留属性大小写、顺序和扩展字段。 */
function RetentionPoliciesEditor({ fieldKey, rows, disabled, onChange }: { fieldKey: string; rows: RetentionPolicyDraftRow[]; disabled: boolean; onChange: (value: string) => void }) {
  const presentation = configurationFieldPresentation(`${fieldKey}:Name`);
  const dayPresentation = configurationFieldPresentation(`${fieldKey}:RetentionDays`);
  const options = retentionPolicyNames.map(value => ({ value, label: presentation.choiceLabels?.[value] ?? value }));
  const nextName = retentionPolicyNames.find(name => !rows.some(row => row.value[row.nameKey] === name));
  const write = (values: RetentionPolicyDraftRow['value'][]) => onChange(JSON.stringify(values, null, 2));
  const change = (index: number, key: string, value: string | number | null) => write(rows.map((row, rowIndex) => rowIndex === index ? { ...row.value, [key]: value } : row.value));
  return <div className="runtime-retention-editor">
    {rows.map((row, index) => <div className="runtime-retention-row" key={index}>
      <Form.Item label={`第 ${index + 1} 项保留对象`} htmlFor={`runtime-${fieldKey}:${index}:Name`}>
        <Select id={`runtime-${fieldKey}:${index}:Name`} aria-label={`${fieldKey}:${index}:Name`} value={String(row.value[row.nameKey])} options={options} onChange={value => change(index, row.nameKey, value)} />
      </Form.Item>
      <Form.Item label="保留天数" htmlFor={`runtime-${fieldKey}:${index}:RetentionDays`}>
        <ConfigurationNumberPicker id={`runtime-${fieldKey}:${index}:RetentionDays`} aria-label={`${fieldKey}:${index}:RetentionDays`} value={typeof row.value[row.daysKey] === 'number' ? row.value[row.daysKey] as number : null} min={1} max={3650} precision={0} presets={dayPresentation.numericPresets} unit="天" onChange={value => change(index, row.daysKey, value)} />
      </Form.Item>
      <Button aria-label={`删除第 ${index + 1} 条保留策略`} disabled={disabled} onClick={() => write(rows.filter((_, rowIndex) => rowIndex !== index).map(item => item.value))}>移除</Button>
    </div>)}
    <Button disabled={disabled || !nextName} onClick={() => { if (nextName) write([...rows.map(row => row.value), { Name: nextName, RetentionDays: 30 }]); }}>添加保留策略</Button>
  </div>;
}

/** 列表提供已知选项和自定义补充，增删与排序均不要求编辑 JSON。 */
function StringListEditor({ fieldKey, values, suggestions, disabled, onChange }: { fieldKey: string; values: string[]; suggestions?: readonly string[]; disabled: boolean; onChange: (value: string) => void }) {
  const options = [...new Set([...(suggestions ?? []), ...values].filter(Boolean))].map(value => ({ value }));
  const write = (next: string[]) => onChange(JSON.stringify(next, null, 2));
  const move = (index: number, offset: number) => { const next = [...values]; [next[index], next[index + offset]] = [next[index + offset], next[index]]; write(next); };
  return <div className="runtime-list-editor">
    {values.map((value, index) => <div className="runtime-list-row" key={index}>
      <Form.Item label={`第 ${index + 1} 项`} htmlFor={`runtime-${fieldKey}:${index}`}>
        <AutoComplete id={`runtime-${fieldKey}:${index}`} aria-label={`${fieldKey}:${index}`} value={value} options={options} filterOption={false} onChange={next => write(values.map((item, rowIndex) => index === rowIndex ? next : item))} />
      </Form.Item>
      <div className="runtime-list-actions">
        <Button disabled={disabled || index === 0} aria-label={`上移第 ${index + 1} 项`} onClick={() => move(index, -1)}>上移</Button>
        <Button disabled={disabled || index === values.length - 1} aria-label={`下移第 ${index + 1} 项`} onClick={() => move(index, 1)}>下移</Button>
        <Button disabled={disabled} aria-label={`移除第 ${index + 1} 项`} onClick={() => write(values.filter((_, rowIndex) => index !== rowIndex))}>移除</Button>
      </div>
    </div>)}
    <Button disabled={disabled} onClick={() => write([...values, suggestions?.find(item => !values.includes(item)) ?? ''])}>添加列表项</Button>
  </div>;
}

/** 保留草稿和提交版本，轮询仅更新最新快照；旧读取不能覆盖刚保存的结果。 */
export function RuntimeConfigurationPanel({ allowed, authenticated, active, refreshToken, onDraftStateChange }: { allowed: boolean; authenticated: boolean; active: boolean; refreshToken: number; onDraftStateChange: (dirty: boolean, saving: boolean) => void }) {
  const { message } = App.useApp();
  const [state, setState] = useState<EditorState>({ draft: {}, loading: allowed, busy: false });
  const [category, setCategory] = useState<ConfigurationCategory>('online');
  const [search, setSearch] = useState('');
  const sequence = useRef(0);
  const reading = useRef<AbortController | null>(null);
  const saving = useRef(false);
  const mounted = useRef(true);
  const read = useCallback(async () => {
    if (!allowed || saving.current) return;
    reading.current?.abort();
    const controller = new AbortController(); reading.current = controller;
    const current = ++sequence.current;
    setState(previous => ({ ...previous, loading: !previous.latest }));
    try {
      const snapshot = await requestApi<RuntimeConfigurationSnapshot>('/api/operations/configuration/runtime', controller.signal);
      if (controller.signal.aborted || !mounted.current || current !== sequence.current) return;
      setState(previous => ({ ...previous, latest: snapshot, loading: false, error: undefined,
        ...(!hasDraft(previous) ? { base: snapshot, draft: configurationDraft(snapshot.configuration) } : {}) }));
    } catch (error) {
      if (controller.signal.aborted || !mounted.current || current !== sequence.current) return;
      if (error instanceof ApiError && (error.status === 401 || error.status === 403)) window.dispatchEvent(new Event('zeye-session-changed'));
      setState(previous => ({ ...previous, loading: false, error: error instanceof Error ? error : new Error(String(error)) }));
    }
  }, [allowed]);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; sequence.current++; reading.current?.abort(); }; }, []);
  useEffect(() => { if (!allowed) setState({ draft: {}, loading: false, busy: false }); }, [allowed]);
  useEffect(() => {
    if (!active || !allowed) return;
    void read(); const timer = window.setInterval(() => { if (document.visibilityState === 'visible') void read(); }, 5000);
    return () => { window.clearInterval(timer); reading.current?.abort(); };
  }, [read, active, allowed, refreshToken]);
  const dirty = hasDraft(state);
  useEffect(() => {
    onDraftStateChange(dirty, state.busy);
  }, [dirty, state.busy, onDraftStateChange]);
  const fields = useMemo(() => state.base ? configurationFields(state.base.configuration) : [], [state.base]);
  const prepared = useMemo(() => {
    try { return { ...configurationPatch(state.base?.configuration ?? {}, state.draft), error: undefined }; }
    catch (error) { return { changes: {}, changedKeys: [] as string[], error: error instanceof Error ? error.message : '配置格式无效' }; }
  }, [state.base, state.draft]);
  const stale = dirty && state.base?.revision !== state.latest?.revision;
  const snapshot = state.latest;
  const reset = () => { if (snapshot) setState(previous => ({ ...previous, base: snapshot, draft: configurationDraft(snapshot.configuration), error: undefined, saved: undefined })); };
  const update = (key: string, value: string | number | boolean | null) => setState(previous => ({ ...previous, draft: { ...previous.draft, [key]: value }, saved: undefined }));
  const save = async () => {
    if (!allowed || !state.base || state.busy || stale || prepared.error || !prepared.changedKeys.length) return;
    saving.current = true; reading.current?.abort(); sequence.current++;
    setState(previous => ({ ...previous, busy: true, error: undefined, saved: undefined }));
    let conflict = false;
    try {
      const response = await requestApi<RuntimeConfigurationSaved>('/api/operations/configuration/runtime', undefined, {
        method: 'PUT', body: JSON.stringify({ revision: state.base.revision, changes: prepared.changes }),
      });
      if (!mounted.current) return;
      setState({ latest: response.snapshot, base: response.snapshot, draft: configurationDraft(response.snapshot.configuration), loading: false, busy: false, saved: response.result });
      message.success(response.snapshot.restartRequiredKeys.length ? '配置已保存，部分参数将在重启后生效' : response.snapshot.overriddenKeys.length ? '配置已保存，部署覆盖项仍使用环境值' : '配置已保存，在线参数已生效');
    } catch (error) {
      if (!mounted.current) return;
      if (error instanceof ApiError && (error.status === 401 || error.status === 403)) window.dispatchEvent(new Event('zeye-session-changed'));
      conflict = error instanceof ApiError && error.status === 409;
      setState(previous => ({ ...previous, error: error instanceof Error ? error : new Error(String(error)) }));
    } finally {
      saving.current = false;
      if (mounted.current) { setState(previous => ({ ...previous, busy: false })); if (conflict) void read(); }
    }
  };
  if (!allowed) return <Result status="403" title="运行配置仅限超级管理员维护" subTitle="普通账号可按权限查看或修改运维策略。" extra={!authenticated ? <Link to="/access/login?returnTo=%2Fsettings"><Button type="primary">登录超级管理员账号</Button></Link> : undefined} />;
  if (!snapshot || !state.base) return state.loading ? <SectionCard title="读取运行配置"><Skeleton active paragraph={{ rows: 8 }} /></SectionCard> : <ApiFeedback error={state.error} retry={() => void read()} />;
  const isSelected = (field: ConfigurationField) => category === 'online' ? isHotConfigurationField(field, state.base!) : field.category === category;
  const needle = search.trim().toLowerCase();
  const visible = fields.filter(field => isSelected(field) && (!needle || `${field.key} ${configurationFieldPresentation(field.key).label} ${configurationFieldPresentation(field.key).help ?? ''}`.toLowerCase().includes(needle)));
  const groups = new Map<string, ConfigurationField[]>();
  for (const field of visible) {
    const group = field.path.slice(0, -1).join(':');
    const items = groups.get(group) ?? []; items.push(field); groups.set(group, items);
  }
  const activeCategory = configurationCategories.find(item => item.key === category)!;
  const hotCount = prepared.changedKeys.filter(key => fields.some(field => field.key === key && isHotConfigurationField(field, state.base!))).length;
  return <div className="runtime-settings">
    <div className="runtime-summary" aria-live="polite">
      <span><DatabaseOutlined /> 配置存储已连接</span><span><ThunderboltOutlined /> {fields.filter(field => isHotConfigurationField(field, state.base!)).length} 项支持在线更新</span>
      <span className={snapshot.restartRequiredKeys.length ? 'runtime-pending' : ''}><HistoryOutlined /> {snapshot.restartRequiredKeys.length ? `${snapshot.restartRequiredKeys.length} 项待重启` : '当前无待重启配置'}</span>
    </div>
    {snapshot.lastReloadError && <Alert type="error" showIcon message={snapshot.lastReloadError} />}
    {state.error && <Alert type="error" showIcon message={state.error.message} description="当前草稿已保留，可修正后重新保存或刷新配置。" action={<Button size="small" onClick={() => void read()}>刷新</Button>} />}
    {stale && <Alert type="warning" showIcon message="配置已被其他页面或进程修改" description="当前草稿已保留。请先核对修改，再撤销草稿读取最新版本；不会自动覆盖他人的配置。" action={<Button size="small" onClick={reset}>读取最新配置</Button>} />}
    {snapshot.restartRequiredKeys.length > 0 && <Alert type="warning" showIcon message="部分已保存参数等待重启" description={<details><summary>查看待重启字段（{snapshot.restartRequiredKeys.length}）</summary><ul className="runtime-key-list">{snapshot.restartRequiredKeys.map(key => <li key={key}>{key}</li>)}</ul></details>} />}
    {state.saved && <Alert type="success" showIcon message="配置已保存" description={`${state.saved.changedKeys.length} 个字段已提交，并已通知在线参数加载${state.saved.restartRequiredKeys.length ? `；其中 ${state.saved.restartRequiredKeys.length} 个字段需要重启` : ''}${snapshot.overriddenKeys.length ? '；部署覆盖项继续使用环境值' : ''}。`} />}
    <div className="settings-layout">
      <SectionCard className="settings-navigation runtime-navigation" title="配置分类">
        <nav className="settings-category-list" aria-label="运行配置分类">{configurationCategories.map(item => {
          const count = fields.filter(field => item.key === 'online' ? isHotConfigurationField(field, state.base!) : field.category === item.key).length;
          return <button type="button" key={item.key} className={`settings-category${category === item.key ? ' selected' : ''}`} aria-pressed={category === item.key} onClick={() => { setCategory(item.key); setSearch(''); }}>
            <span className="settings-category-copy"><span>{item.name}</span><small>{item.description}</small></span><span className="settings-category-count">{count}</span>
          </button>;
        })}</nav>
      </SectionCard>
      <SectionCard className="runtime-editor-card" title={<div><div className="settings-section-title">{activeCategory.name}</div><div className="settings-section-caption">{activeCategory.description} · {visible.length} 项</div></div>} extra={<Input allowClear prefix={<SearchOutlined />} placeholder="搜索当前分类" aria-label="搜索配置字段" value={search} onChange={event => setSearch(event.target.value)} className="runtime-search" />}>
        <Form layout="vertical" disabled={state.busy} onFinish={() => void save()}>
          {visible.length ? Array.from(groups, ([group, items]) => <section className="runtime-field-group" key={group} aria-label={configurationGroupLabel(items[0].path.slice(0, -1))}>
            <h3>{configurationGroupLabel(items[0].path.slice(0, -1)) || '通用配置'}</h3>
            <div className="runtime-field-grid">{items.map(field => {
              const presentation = configurationFieldPresentation(field.key);
              const inputId = `runtime-${field.key}`;
              const overridden = snapshot.overriddenKeys.some(key => configurationKeyMatches(key, field.key));
              const pending = snapshot.restartRequiredKeys.some(key => configurationKeyMatches(key, field.key));
              const value = state.draft[field.key];
              const selectedChoice = presentation.choices?.find(option => String(option).toLowerCase() === String(value ?? '').toLowerCase());
              const policyRows = field.key.toLowerCase() === retentionPolicyConfigurationKey.toLowerCase() ? configurationRetentionPolicyRows(value) : undefined;
              const list = Array.isArray(field.value) && (presentation.listSuggestions || (field.value.length > 0 && field.value.every(item => typeof item === 'string'))) ? configurationStringList(value) : undefined;
              const temporal = configurationTemporalKind(field.key);
              const validTemporal = temporal && isValidConfigurationTemporalValue(temporal, value);
              const parsedTime = validTemporal ? dayjs(temporal === 'time' ? `2000-01-01T${String(value).trim()}` : String(value).trim()) : null;
              // Day.js 构造器会将 1～99 年映射到 1901～1999，显示时恢复配置中的原始年份。
              const pickerValue = temporal === 'datetime' && parsedTime ? parsedTime.year(Number(String(value).trim().slice(0, 4))) : parsedTime;
              const hot = isHotConfigurationField(field, state.base!);
              const effective = snapshot.effectiveConfiguration[field.key];
              return <div className={`runtime-field${prepared.changedKeys.includes(field.key) ? ' changed' : ''}${Array.isArray(field.value) ? ' runtime-field-wide' : ''}`} key={field.key}>
                <Form.Item htmlFor={inputId} label={<span className="runtime-field-label">{presentation.label}{presentation.unit && `（${presentation.unit}）`}<Tag color={hot ? 'green' : undefined}>{hot ? '在线生效' : '重启生效'}</Tag></span>} extra={<span className="runtime-field-key">{field.key}</span>}>
                  {typeof field.value === 'boolean' ? <Switch id={inputId} aria-label={field.key} checked={value === true} checkedChildren="开启" unCheckedChildren="关闭" onChange={checked => update(field.key, checked)} />
                    : presentation.choices && (typeof field.value !== 'number' || typeof presentation.choices[0] === 'number') ? <Select id={inputId} aria-label={field.key} value={selectedChoice ?? (typeof value === 'number' ? value : String(value ?? ''))} options={presentation.choices.map(option => ({ value: option, label: presentation.choiceLabels?.[option] ?? String(option) }))} onChange={next => update(field.key, typeof field.value === 'number' ? Number(next) : String(next))} />
                      : policyRows ? <RetentionPoliciesEditor fieldKey={field.key} rows={policyRows} disabled={state.busy} onChange={next => update(field.key, next)} />
                        : list ? <StringListEditor fieldKey={field.key} values={list} suggestions={presentation.listSuggestions} disabled={state.busy} onChange={next => update(field.key, next)} />
                          : temporal === 'time' ? <TimePicker id={inputId} aria-label={field.key} value={pickerValue} locale={pickerLocale} placeholder="请选择时间" inputReadOnly format="HH:mm:ss" onChange={next => update(field.key, next?.format('HH:mm:ss') ?? '')} />
                            : temporal === 'datetime' ? <DatePicker id={inputId} aria-label={field.key} value={pickerValue} locale={pickerLocale} classNames={{ popup: { root: 'runtime-datetime-popup' } }} placeholder="请选择日期和时间" inputReadOnly showTime format={localDateTimeFormat} onChange={next => update(field.key, next?.format('YYYY-MM-DDTHH:mm:ss') ?? '')} />
                              : typeof field.value === 'number' ? <ConfigurationNumberPicker id={inputId} aria-label={field.key} value={typeof value === 'number' ? value : null} min={presentation.min} max={presentation.max} step={presentation.step ?? (field.value % 1 ? 0.1 : 1)} presets={presentation.numericPresets} unit={presentation.unit} onChange={next => update(field.key, next)} />
                                : Array.isArray(field.value) ? <Input.TextArea id={inputId} aria-label={field.key} value={String(value ?? '')} rows={4} spellCheck={false} onChange={event => update(field.key, event.target.value)} />
                                  : <Input id={inputId} aria-label={field.key} value={String(value ?? '')} onChange={event => update(field.key, event.target.value)} />}
                </Form.Item>
                {presentation.help && <p className="settings-field-help">{presentation.help}</p>}
                {temporal && value && !validTemporal && <p className="settings-field-help">当前旧值：{String(value)}。选择日期或时间后替换；未修改时仍保留原值。</p>}
                {(overridden || pending) && <div className="runtime-effective"><Tag color={overridden ? 'blue' : 'orange'}>{overridden ? '部署环境覆盖' : '等待重启'}</Tag>{effective !== undefined && <span>当前生效值：{effective ?? '空'}</span>}{overridden && <small>修改会保存到配置库；当前仍优先使用部署覆盖值。</small>}</div>}
              </div>;
            })}</div>
          </section>) : <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={needle ? '没有匹配的配置字段' : '该分类暂无配置字段'} />}
          {prepared.error && <Alert type="error" showIcon message={prepared.error} />}
          {dirty && !prepared.error && prepared.changedKeys.length > 0 && <details className="runtime-change-preview"><summary>本次修改 {prepared.changedKeys.length} 项：{hotCount} 项支持在线更新，{prepared.changedKeys.length - hotCount} 项需要重启</summary><ul className="runtime-key-list">{prepared.changedKeys.map(key => <li key={key}>{configurationFieldPresentation(key).label}<small>{key}</small></li>)}</ul></details>}
          <div className="settings-policy-footer runtime-save-bar">
            <div className={`settings-form-status${dirty ? ' pending' : ''}`} role="status">{dirty ? <><span className="settings-status-dot" />{prepared.error ? '有无效的配置内容，请先修正' : `${prepared.changedKeys.length} 项未保存修改`}</> : <><CheckCircleOutlined />配置已同步</>}</div>
            <div className="settings-policy-actions"><Button icon={<UndoOutlined />} onClick={reset} disabled={state.busy || !dirty}>撤销修改</Button><Button type="primary" htmlType="submit" icon={<SaveOutlined />} loading={state.busy} disabled={!dirty || stale || Boolean(prepared.error) || !prepared.changedKeys.length}>保存配置</Button></div>
          </div>
        </Form>
        <div className="settings-config-source"><div><span>配置存储</span><p>{snapshot.storagePath}</p></div><div><span>独立配置入口</span><p><Link to="/settings/fusion">Fusion 接入配置</Link>与规则管理页面支持在线维护；启动引导参数保留在 appsettings.json。</p></div></div>
        <Button type="link" size="small" icon={<ReloadOutlined />} onClick={() => void read()} disabled={state.busy}>重新读取配置</Button>
      </SectionCard>
    </div>
  </div>;
}
