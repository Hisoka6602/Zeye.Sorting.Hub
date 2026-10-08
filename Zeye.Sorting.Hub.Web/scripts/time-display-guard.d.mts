/** 编译守卫的问题位置与中文说明。 */
export interface TimeDisplayIssue { file: string; line: number; column: number; message: string }
/** 检查真实源码渲染位置，支持单文件回归。 */
export function inspectTimeDisplay(source: string, filename?: string): TimeDisplayIssue[];
/** 全量校验前端源码；存在违规时阻止编译。 */
export function assertTimeDisplay(root?: string): void;
