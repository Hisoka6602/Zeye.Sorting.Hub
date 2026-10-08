"""仅在专属验收容器执行成功写入、版本冲突、异步入队和密码确认清理。"""
from datetime import datetime
import json
import time
from joint_business_assertions import expect_status


def verify_business_writes(api, docker, service):
    """使用正式 HTTP 入口写入，清理范围由 EF 工具预检查，结果不包含认证凭据。"""
    state = json.loads(docker('inspect', service))[0]
    assert state['Config']['Labels']['com.docker.compose.project'] == 'zeye-db-verification'
    assert ':519' in api.base, '写入验收只允许隔离 Hub 端口'
    checks = []
    now = datetime.now().strftime('%Y-%m-%dT%H:%M:%S')
    base = time.time_ns() // 100
    body = dict(id=str(base), parcelTimestamp=str(base + 621355968000000000), type=0,
                barCodes='JOINT-CRUD-' + str(base), weight=1.25, workstationName='隔离业务写入验收',
                scannedTime=now, dischargeTime=now, targetChuteId='13', actualChuteId='13', requestStatus=1,
                length=300, width=200, height=100, volume=6000000)
    created = api.request('/api/admin/parcels', body)
    try:
        assert str(created['id']) == body['id'] and created['barCodes'] == body['barCodes']
        expect_status(api, '/api/admin/parcels', 200, body)
        expect_status(api, '/api/admin/parcels', 409, dict(body, weight=1.5))
        updated = api.request('/api/admin/parcels/' + body['id'], dict(operation=1, completedTime=now), 'PUT')
        assert updated['status'] == 1  # ParcelDetailResponse 使用公开的整数状态合同（Completed=1）。
    finally:
        # 即使断言发现问题，也只删除本次成功创建的临时票；保留联调输入及失败日志。
        expect_status(api, '/api/admin/parcels/' + body['id'], 204, method='DELETE')
    expect_status(api, '/api/parcels/' + body['id'], 404)
    checks.append('createReplayPayloadConflictUpdateDelete')

    batch = [dict(body, id=str(base + offset), barCodes=body['barCodes'] + '-' + str(offset)) for offset in (10, 11, 12)]
    queued = api.request('/api/admin/parcels/batch-buffer', dict(parcels=batch))
    assert queued['acceptedCount'] == 3 and queued['rejectedCount'] == 0
    deadline = time.monotonic() + 30
    while True:
        try:
            persisted = [api.request('/api/parcels/' + item['id']) for item in batch]
            assert all(str(item['id']) == request['id'] for item, request in zip(persisted, batch))
            break
        except AssertionError:
            if time.monotonic() >= deadline:
                raise
            time.sleep(0.5)
    for item in batch:
        expect_status(api, '/api/admin/parcels/' + item['id'], 204, method='DELETE')
    checks.append('bufferedBatchActuallyPersisted')

    rules = api.request('/api/operations/rules/exception')
    saved = api.request('/api/operations/rules/exception',
                        dict(expectedRevision=rules['revision'], rules=rules['rules']), 'PUT')
    assert saved['rules'] == rules['rules'] and saved['revision'] == rules['revision'] + 1
    expect_status(api, '/api/operations/rules/exception', 409,
                  dict(expectedRevision=rules['revision'], rules=rules['rules']), 'PUT')
    expect_status(api, '/api/operations/rules/exception', 400,
                  dict(expectedRevision=saved['revision'], rules=[]), 'PUT')
    checks.append('ruleVersionConflictAndSystemFallbackProtection')

    task = api.request('/api/data-governance/archive-tasks',
                       dict(taskType='WebRequestAuditLogHistory', retentionDays=3650,
                            requestedBy='joint-verification', remark='仅验收 dry-run，保留所有审计数据'))
    assert task['isDryRun'] and task['status'] in ('Pending', 'Running', 'Completed')

    def await_archive():
        deadline = time.monotonic() + 90
        while time.monotonic() < deadline:
            listing = api.request('/api/data-governance/archive-tasks?pageNumber=1&pageSize=100')
            current = next(item for item in listing['items'] if item['id'] == task['id'])
            assert current['status'] != 'Failed', '归档演练失败：' + current['failureMessage']
            if current['status'] == 'Completed':
                assert current['isDryRun'] and current['processedItemCount'] == 0
                return current
            time.sleep(1)
        raise AssertionError('归档演练未在 90 秒内完成')

    await_archive()
    retried = api.request('/api/data-governance/archive-tasks/' + str(task['id']) + '/retry', {})
    assert retried['status'] in ('Pending', 'Running', 'Completed')
    await_archive()
    checks.append('archiveDryRunCompletionAndRetry')

    fixture = json.loads(docker('exec', service, 'dotnet', '/app/verification/DatabaseVerification.dll',
                                '--seed-cleanup-case', timeout=90).strip().splitlines()[-1])
    cleanup = dict(createdBefore=fixture['createdBefore'])
    expect_status(api, '/api/admin/parcels/cleanup-expired', 400, cleanup)
    expect_status(api, '/api/admin/parcels/cleanup-expired', 400, dict(cleanup, password='WrongPassword_ForTests'))
    deleted = api.request('/api/admin/parcels/cleanup-expired',
                          dict(cleanup, password='MatrixAdministrator20261007_ForTests'))
    assert deleted['decision'] == 'execute' and deleted['plannedCount'] == fixture['count'] == deleted['executedCount']
    record_id = deleted['cleanupRecordId']
    detail = api.request('/api/admin/parcels/cleanup-history/' + record_id)
    record = detail['record']
    assert record['operator']['account'] == 'matrix-admin' and record['executedCount'] == 2
    assert 'items' not in detail and all(item not in json.dumps(detail) for item in fixture['ids'])
    assert 'MatrixAdministrator' not in json.dumps(detail) and 'JOINT-CLEANUP' not in json.dumps(detail)
    for identity in fixture['ids']:
        expect_status(api, '/api/parcels/' + identity, 404)
    assert any(item['id'] == record_id for item in api.request('/api/admin/parcels/cleanup-history')['items'])
    checks.append('passwordConfirmedCleanupAndPermanentSummaryAudit')
    return dict(checks=checks, temporaryCrudParcelsRemoved=4, cleanupFixtureParcelsRemoved=2,
                cleanupRecordId=record_id, archiveTaskId=str(task['id']), permanentSummaryOnly=True)
