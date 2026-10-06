/** 后端真实包裹合同，所有64位编号按字符串消费。 */
export interface ParcelSummary {
  sourceInstanceId: string | null;
  sourceRunId: string | null;
  sourceParcelId: string | null;
  detectedTime: string | null;
  measurementTime: string | null;
  targetChuteCode: string | null;
  actualChuteCode: string | null;
  taskCode: string | null;
  volumetricWeightGrams: number | null;
  isFallbackChuteAssigned: boolean | null;
  isRoutingBlocked: boolean | null;
  sourceExceptionCode: string | null;
  id: string;
  createdTime: string;
  modifyTime: string;
  modifyIp: string;
  parcelTimestamp: string;
  type: number;
  status: number;
  exceptionType: number | null;
  noReadType: number;
  sorterCarrierId: string | null;
  segmentCodes: string | null;
  lifecycleMilliseconds: number | null;
  targetChuteId: string | null;
  actualChuteId: string | null;
  barCodes: string;
  weight: number | null;
  requestStatus: number;
  bagCode: string;
  workstationName: string;
  isSticking: boolean;
  length: number | null;
  width: number | null;
  height: number | null;
  volume: number | null;
  scannedTime: string;
  dischargeTime: string | null;
  completedTime: string | null;
  hasImages: boolean;
  hasVideos: boolean;
  coordinate: string;
}

/** 已持久化来源处理事实，未知量测保留null。 */
export interface ParcelProcessingRecord {
  previousCreationGapMilliseconds: number | null;
  isSpacingViolation: boolean | null;
  isAwaitingWcsDecision: boolean | null;
  workstationName: string | null;
  recordId: string;
  sourceInstanceId: string;
  sourceRunId: string;
  sourceParcelId: string | null;
  parcelId: string | null;
  stage: number;
  occurredAt: string;
  recordedAt: string;
  partitionTime: string;
  isSuccess: boolean | null;
  attemptNumber: number;
  barcode: string | null;
  barcodesJson: string | null;
  weightGrams: number | null;
  lengthMm: number | null;
  widthMm: number | null;
  heightMm: number | null;
  volumeMm3: number | null;
  volumetricWeightGrams: number | null;
  receivedAt: string | null;
  measuredAt: string | null;
  hasReliableTimestamp: boolean | null;
  hasReliableFrameBoundary: boolean | null;
  correlationId: string | null;
  triggerBatch: string | null;
  scanSequence: string | null;
  messageIdentity: string | null;
  bindingMode: string | null;
  candidateSourceParcelId: string | null;
  finalSourceParcelId: string | null;
  deltaMilliseconds: number | null;
  decisionReason: string | null;
  fifoRecoveryMode: string | null;
  provider: string | null;
  taskCode: string | null;
  targetChuteCode: string | null;
  dispatchedChuteCode: string | null;
  actualChuteCode: string | null;
  isFallback: boolean | null;
  isRoutingBlocked: boolean | null;
  exceptionCode: string | null;
  errorMessage: string | null;
  rawPayload: string | null;
  requestUrl: string | null;
  requestHeaders: string | null;
  requestBody: string | null;
  responseBody: string | null;
  responseStatusCode: number | null;
  requestAt: string | null;
  responseAt: string | null;
  elapsedMilliseconds: number | null;
  imagePath: string | null;
  imageCamera: string | null;
  imageContentHash: string | null;
}

/** 完整详情包含全部已有值对象和新增处理事实。 */
export interface ParcelDetail extends ParcelSummary {
  processingRecords: ParcelProcessingRecord[];
  barCodeInfos: Record<string, unknown>[];
  weightInfos: Record<string, unknown>[];
  apiRequests: Record<string, unknown>[];
  commandInfos: Record<string, unknown>[];
  imageInfos: Record<string, unknown>[];
  videoInfos: Record<string, unknown>[];
  volumeInfo: Record<string, unknown> | null;
  chuteInfo: Record<string, unknown> | null;
  sorterCarrierInfo: Record<string, unknown> | null;
  bagInfo: Record<string, unknown> | null;
  deviceInfo: Record<string, unknown> | null;
  grayDetectorInfo: Record<string, unknown> | null;
  stickingParcelInfo: Record<string, unknown> | null;
  parcelPositionInfo: Record<string, unknown> | null;
}

/** 服务端分页合同。 */
export interface ParcelList { items: ParcelSummary[]; pageNumber: number; pageSize: number; totalCount: number; hasTotalCount: boolean }

/** 图片查询只在用户查看时加载，地址可能为对象存储临时签名。 */
export interface ParcelImage { cameraName: string; sourcePath: string; url: string | null; unavailableReason: string | null }
export interface ParcelImages { parcelId: string; hasImages: boolean; images: ParcelImage[] }

/** 来源阶段中文名称，与合同Stage数值一一对应。 */
export const processingStages = ['分拣机检测', 'DWS 接收', 'DWS 绑定', '扫描上传', '格口分配', '分拣指令', '实际落格', '设备异常', '落格上报', '图片登记', '图片上传'] as const;

/** 完整字段中文标签，直接对应后端数据合同。 */
export const parcelFieldLabels: Record<string, string> = {
  "previousCreationGapMilliseconds": "与前包间隔（ms）",
  "isSpacingViolation": "间距违规",
  "isAwaitingWcsDecision": "等待 WCS 决策",
  "sourceInstanceId": "来源实例编码",
  "sourceRunId": "编号会话",
  "sourceParcelId": "来源设备包裹号",
  "detectedTime": "首次检测时间",
  "measurementTime": "测量时间",
  "targetChuteCode": "原始目标格口编码",
  "actualChuteCode": "设备实际落格编码",
  "taskCode": "外部任务号",
  "volumetricWeightGrams": "体积重量（g）",
  "isFallbackChuteAssigned": "是否使用兜底格口",
  "isRoutingBlocked": "是否禁止继续正常路由",
  "sourceExceptionCode": "设备异常原始编码",
  "id": "包裹 ID",
  "createdTime": "创建时间",
  "modifyTime": "修改时间",
  "modifyIp": "修改 IP",
  "parcelTimestamp": "包裹时间戳",
  "type": "包裹类型",
  "status": "包裹状态",
  "exceptionType": "分拣异常类型",
  "noReadType": "NoRead 类型",
  "sorterCarrierId": "小车编号",
  "segmentCodes": "三段码",
  "lifecycleMilliseconds": "生命周期（ms）",
  "targetChuteId": "目标格口 ID",
  "actualChuteId": "实际格口 ID",
  "barCodes": "主条码",
  "weight": "重量（kg）",
  "requestStatus": "扫描上传状态",
  "bagCode": "集包号",
  "workstationName": "来源工作台名称",
  "isSticking": "是否叠包",
  "length": "长度（mm）",
  "width": "宽度（mm）",
  "height": "高度（mm）",
  "volume": "物理体积（mm³）",
  "scannedTime": "扫码时间",
  "dischargeTime": "落格时间",
  "completedTime": "包裹完结时间",
  "hasImages": "是否有图片",
  "hasVideos": "是否有视频",
  "coordinate": "包裹坐标",
  "apiType": "接口类型",
  "requestUrl": "外部接口地址",
  "queryParams": "参数",
  "headers": "协议头",
  "requestBody": "外部接口请求体",
  "responseBody": "外部接口响应体",
  "requestTime": "请求时间",
  "responseTime": "响应时间",
  "elapsedMilliseconds": "耗时（ms）",
  "exception": "异常信息",
  "rawData": "判断源数据内容",
  "formattedMessage": "格式化说明",
  "chuteId": "格口 ID",
  "chuteName": "格口名称",
  "parcelCount": "当前集包中包裹数量",
  "baggingTime": "集包完成时间",
  "barCode": "条码",
  "barCodeType": "条码类型",
  "capturedTime": "采集时间",
  "backupChuteId": "备用格口 Id",
  "landedTime": "落格时间",
  "protocolType": "协议类型",
  "protocolName": "协议名称",
  "connectionName": "连接名称",
  "commandPayload": "指令内容",
  "generatedTime": "指令产生时间",
  "actionType": "指令作用类型",
  "direction": "指令方向",
  "carrierNumber": "小车编号",
  "attachBoxInfo": "附加框信息",
  "mainBoxInfo": "主框信息",
  "linkedCarrierCount": "联动小车数量",
  "centerPosition": "包裹中心点坐标",
  "resultTime": "检测结果返回时间",
  "rawResult": "原始返回数据内容",
  "cameraName": "相机名称",
  "customName": "设备自定义名称",
  "cameraSerialNumber": "相机序列号",
  "imageType": "图片类型",
  "relativePath": "图片相对路径",
  "captureType": "图片获取方式",
  "machineCode": "设备机器码",
  "x1": "最小 X 坐标",
  "x2": "最大 X 坐标",
  "y1": "最小 Y 坐标",
  "y2": "最大 Y 坐标",
  "backgroundX1": "背景区域最小 X 坐标",
  "backgroundX2": "背景区域最大 X 坐标",
  "backgroundY1": "背景区域最小 Y 坐标",
  "backgroundY2": "背景区域最大 Y 坐标",
  "loadedTime": "包裹上车时间",
  "conveyorSpeedWhenLoaded": "上车时输送带速度",
  "receiveTime": "判断结果接收时间",
  "channel": "视频通道",
  "nvrSerialNumber": "NVR 序列号",
  "nodeType": "节点类型",
  "sourceType": "数据来源类型",
  "rawVolume": "原始体积字符串",
  "evidenceCode": "取证依据",
  "formattedLength": "长度（mm）",
  "formattedWidth": "宽度（mm）",
  "formattedHeight": "高度（mm）",
  "formattedVolume": "体积（cm³）",
  "adjustedLength": "长度调整值",
  "adjustedWidth": "宽度调整值",
  "adjustedHeight": "高度调整值",
  "adjustedVolume": "体积调整值",
  "bindTime": "体积绑定时间",
  "rawWeight": "原始重量",
  "formattedWeight": "重量（kg）",
  "weighingTime": "称重时间",
  "adjustedWeight": "调整后的重量",
  "key": "来源实例、会话与记录标识计算得到的稳定持久化主键",
  "recordId": "记录标识",
  "parcelId": "包裹 ID",
  "stage": "处理阶段",
  "occurredAt": "发生时间",
  "recordedAt": "入库时间",
  "partitionTime": "固定分表时间",
  "payloadHash": "规范化载荷哈希",
  "isSuccess": "本次结果",
  "attemptNumber": "尝试次数",
  "barcode": "主条码",
  "barcodesJson": "多条码列表",
  "weightGrams": "原始测量重量（g）",
  "lengthMm": "测量长度（mm）",
  "widthMm": "测量宽度（mm）",
  "heightMm": "测量高度（mm）",
  "volumeMm3": "物理体积（mm³）",
  "receivedAt": "来源程序捕获的报文接收时间",
  "measuredAt": "设备原始测量时间",
  "hasReliableTimestamp": "时间戳可靠",
  "hasReliableFrameBoundary": "帧边界可靠",
  "correlationId": "设备共同关联号",
  "triggerBatch": "设备触发批次",
  "scanSequence": "设备扫描序号",
  "messageIdentity": "DWS稳定消息身份",
  "bindingMode": "融合关联方式",
  "candidateSourceParcelId": "融合候选设备包裹号",
  "finalSourceParcelId": "融合最终设备包裹号",
  "deltaMilliseconds": "检测与报文时间差（ms）",
  "decisionReason": "绑定拒绝、兜底或处理结果原因",
  "fifoRecoveryMode": "FIFO恢复模式",
  "provider": "业务Provider标识",
  "dispatchedChuteCode": "最终实际下发的格口编码",
  "isFallback": "是否采用兜底格口",
  "exceptionCode": "来源异常代码",
  "errorMessage": "本次执行失败说明",
  "rawPayload": "原始设备报文或通信指令",
  "requestHeaders": "外部接口请求头",
  "responseStatusCode": "外部接口响应状态码",
  "requestAt": "外部接口请求时间",
  "responseAt": "外部接口响应时间",
  "imagePath": "图片文件路径或对象存储键",
  "imageCamera": "图片来源相机标识",
  "imageContentHash": "图片文件完整性哈希"
};
