import { Alert, Button } from 'antd';

/** 服务失败时保留失败信息和重试入口，不使用示例填补结果。 */
export function ApiFeedback({ error, retry }: { error?: Error; retry?: () => void }) {
  return error ? <Alert type="error" showIcon message="请求失败" description={error.message} action={retry && <Button onClick={retry}>重试</Button>} style={{ marginBottom: 16 }} /> : null;
}
