/** 一次设备包裹计数有效会话内的检测身份；条码和时间不参与身份。 */
export interface DetectionSourceIdentity {
  sourceInstanceId: string;
  sourceRunId: string;
  sourceParcelId: string;
}

/** 根据不可变来源三元组生成可跨页面重载重复计算的检测记录标识。 */
export async function createDetectionRecordId(identity: DetectionSourceIdentity): Promise<string> {
  const { sourceInstanceId, sourceRunId, sourceParcelId } = identity;
  if ([sourceInstanceId, sourceRunId].some(value => !value || value !== value.trim() || /[\u0000-\u001f\u007f-\u009f]/u.test(value))) {
    throw new Error('来源实例和设备编号会话必须是稳定且无首尾空白的标识');
  }
  if (!/^[1-9]\d*$/u.test(sourceParcelId) || BigInt(sourceParcelId) > 9223372036854775807n) {
    throw new Error('来源包裹编号必须是有效的正整数');
  }
  if (!globalThis.crypto?.subtle) {
    throw new Error('当前环境无法生成稳定检测标识，请使用安全连接');
  }
  const identityBytes = new TextEncoder().encode(JSON.stringify(['detected-v1', sourceInstanceId, sourceRunId, sourceParcelId]));
  const digest = new Uint8Array(await globalThis.crypto.subtle.digest('SHA-256', identityBytes));
  return `detected-v1-${Array.from(digest, byte => byte.toString(16).padStart(2, '0')).join('')}`;
}
