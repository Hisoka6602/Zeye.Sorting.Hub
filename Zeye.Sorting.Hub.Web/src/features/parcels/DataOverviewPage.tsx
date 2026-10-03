import { PageIntro } from '../../components/PageIntro';
import { WorkbenchDataOverview } from './WorkbenchDataOverview';

export function DataOverviewPage() {
  return <div className="data-overview-page">
    <PageIntro title="数据概览" description="查看包裹入库、分拣状态与每日完成趋势。" />
    <WorkbenchDataOverview />
  </div>;
}
