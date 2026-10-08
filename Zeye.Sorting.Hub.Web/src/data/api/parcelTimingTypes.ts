import type { ParcelProcessingRecord, ParcelSummary } from './parcelTypes';

/** 时序只读投影；所有中心与来源64位编号按字符串使用。 */
export type ParcelTimingCandidate = Pick<ParcelSummary, 'id' | 'barCodes' | 'workstationName' | 'sourceInstanceId' | 'sourceRunId' | 'sourceParcelId' | 'status' | 'scannedTime' | 'detectedTime' | 'createdTime' | 'dischargeTime' | 'completedTime'>;

/** 重复条码候选由服务端精确匹配并分页，不能自动选取第一票。 */
export interface ParcelTimingCandidates { items: ParcelTimingCandidate[]; pageNumber: number; pageSize: number; totalCount: number }

/** 历史接口的真实请求和响应时间，耗时不用于补造缺失响应。 */
export interface ParcelTimingApiRequest { apiType: number; requestStatus: number; requestTime: string; responseTime: string | null; elapsedMilliseconds: number }

/** 时序事实只携带时间、状态与精简关联元数据，完整报文通过详情页查看。 */
export interface ParcelTimingParcel extends ParcelTimingCandidate { processingRecords: ParcelProcessingRecord[]; apiRequests: ParcelTimingApiRequest[] }

/** 固定两侧最多各5票，数量是实际存在的记录数，items包括锚点。 */
export interface ParcelTiming { anchorId: string; beforeCount: number; afterCount: number; items: ParcelTimingParcel[] }
