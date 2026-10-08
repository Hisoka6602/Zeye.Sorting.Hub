import { Anchor, Button, Collapse, Empty, Input, Steps, Tag } from 'antd';
import { DownOutlined, SearchOutlined } from '@ant-design/icons';
import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { InfoAlert } from '../components/InfoAlert';
import { PageIntro } from '../components/PageIntro';
import { VectorIcon } from '../components/VectorIcon';
import { helpFaqs, helpGroups, type HelpTopic } from './helpContent';
import './help.css';

const commonTasks = [
  { key: 'parcels', title: '查找包裹' },
  { key: 'timing', title: '比较节点时差' },
  { key: 'duration', title: '定位长耗时包裹' },
  { key: 'fusion', title: '接入 Fusion 工作台' },
];

function matchesTopic(topic: HelpTopic, keyword: string) {
  return [topic.title, topic.summary, topic.entry, topic.access ?? '',
    ...topic.steps.flatMap(step => [step.title, step.description]),
    ...topic.notes.flatMap(note => [note.title, note.text]),
  ].join(' ').toLocaleLowerCase().includes(keyword);
}

/** 指南按业务任务组织，步骤、统计口径和限制均可检索。 */
export function HelpPage() {
  const [open, setOpen] = useState<string[]>(['parcels']);
  const [search, setSearch] = useState('');
  const [searchOpen, setSearchOpen] = useState<string[]>([]);
  const [scrollTarget, setScrollTarget] = useState<string | null>(null);
  const keyword = search.trim().toLocaleLowerCase();
  const groups = helpGroups.map(group => ({ ...group, topics: group.topics.filter(topic => !keyword || matchesTopic(topic, keyword)) }))
    .filter(group => group.topics.length > 0);
  const topicCount = groups.reduce((total, group) => total + group.topics.length, 0);

  const updateSearch = (value: string) => {
    setSearch(value);
    setSearchOpen(helpGroups.flatMap(group => group.topics.filter(topic => matchesTopic(topic, value.trim().toLocaleLowerCase())).map(topic => topic.key)));
  };

  const jump = (key: string) => {
    setSearch('');
    const group = helpGroups.find(item => item.key === key);
    const topicKey = group?.topics[0].key ?? (helpGroups.some(item => item.topics.some(topic => topic.key === key)) ? key : undefined);
    if (topicKey) setOpen(keys => [...new Set([...keys, topicKey])]);
    setScrollTarget(group ? `group-${key}` : topicKey ? `topic-${key}` : key);
  };
  // 先提交搜索/展开状态，再定位已经渲染的标题。
  useEffect(() => {
    if (!scrollTarget) return;
    const frame = requestAnimationFrame(() => {
      document.getElementById(scrollTarget)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      setScrollTarget(null);
    });
    return () => cancelAnimationFrame(frame);
  }, [scrollTarget]);

  return <div className="help-grid help-page">
    <div className="help-main">
      <PageIntro title="操作指南" description="从已保存的包裹数据出发，了解查询、分析、事实追溯与系统维护的实际操作。" />
      <InfoAlert closable={false} message="先定位包裹，再用分析与时序追溯处理过程"
        description="业务数据由工作台或 Fusion 自动传入。各节列出页面入口、操作步骤、统计口径和权限要求；数据以当前服务端保存的记录为准。" />

      <section aria-labelledby="overview">
        <h2 className="card-heading" id="overview">功能概览</h2>
        <div className="help-tiles">{helpGroups.map(group => <button type="button" className="help-tile" key={group.key} onClick={() => jump(group.key)}>
          <span className="help-tile-icon"><VectorIcon name={group.icon} surface="help" size={28} /></span>
          <span className="help-tile-copy"><span className="help-tile-title">{group.title}</span><span className="help-tile-caption">{group.summary}</span></span>
        </button>)}</div>
        <nav className="help-common-tasks" aria-label="常用任务"><span>常用任务</span>{commonTasks.map(task => <Button type="link" key={task.key} onClick={() => jump(task.key)}>{task.title}</Button>)}</nav>
      </section>

      <section className="help-detail" aria-labelledby="instructions">
        <div className="help-detail-heading">
          <h2 className="card-heading" id="instructions">操作说明</h2>
          <Input allowClear prefix={<SearchOutlined aria-hidden />} value={search} onChange={event => updateSearch(event.target.value)}
            aria-label="搜索操作指南" placeholder="搜索功能、步骤或限制" />
        </div>
        {keyword && <p className="help-search-result" role="status">找到 {topicCount} 项相关功能，已展开操作说明。</p>}
        {groups.length ? groups.map(group => <section key={group.key} className="help-topic-group" aria-labelledby={`group-${group.key}`}>
          <h3 id={`group-${group.key}`} className="help-group-heading"><VectorIcon name={group.icon} size={18} />{group.title}<span>{group.topics.length} 项功能</span></h3>
          <Collapse className="help-collapse" expandIconPosition="end" expandIcon={() => <DownOutlined />} activeKey={keyword ? searchOpen : open}
            onChange={keys => (keyword ? setSearchOpen : setOpen)(current => [...current.filter(key => !group.topics.some(topic => topic.key === key)), ...keys.map(String)])}
            items={group.topics.map(topic => ({
              key: topic.key,
              label: <span className="help-collapse-label" id={`topic-${topic.key}`}><b>{topic.title}</b><span className="help-collapse-summary">{topic.summary}</span></span>,
              children: <div className="help-topic-content">
                <div className="help-topic-entry"><span><b>页面入口</b>{topic.entry}</span><div className="help-topic-links">{topic.links.map(link => <Link key={link.to} to={link.to}>{link.title} →</Link>)}</div></div>
                {topic.access && <div className="help-topic-access"><Tag>权限要求</Tag><span>{topic.access}</span></div>}
                <div className="help-step-box"><b>操作步骤</b><Steps size="small" direction="vertical" current={-1} items={topic.steps} /></div>
                <dl className="help-topic-notes">{topic.notes.map(note => <div key={note.title}><dt>{note.title}</dt><dd>{note.text}</dd></div>)}</dl>
              </div>,
            }))} />
        </section>) : <Empty description="没有匹配的操作说明" image={Empty.PRESENTED_IMAGE_SIMPLE}><Button onClick={() => setSearch('')}>清除搜索</Button></Empty>}
      </section>

      <section className="help-faq" aria-labelledby="faq">
        <h2 className="card-heading" id="faq">常见问题</h2>
        <Collapse className="help-collapse" expandIconPosition="end" items={helpFaqs.map(faq => ({ key: faq.key, label: faq.title, children: <p>{faq.answer}</p> }))} />
      </section>
    </div>
    <aside className="help-toc" aria-label="操作指南目录"><b>本页目录</b><Anchor affix={false} getCurrentAnchor={current => current || '#overview'}
      items={[{ key: 'overview', href: '#overview', title: '功能概览' }, ...groups.map(group => ({ key: group.key, href: `#group-${group.key}`, title: group.title,
        children: group.topics.map(topic => ({ key: topic.key, href: `#topic-${topic.key}`, title: topic.title })),
      })), { key: 'faq', href: '#faq', title: '常见问题' }]}
      onClick={(event, link) => { event.preventDefault(); jump(link.href.replace(/^#(?:topic-|group-)?/, '')); }} />
    </aside>
  </div>;
}
