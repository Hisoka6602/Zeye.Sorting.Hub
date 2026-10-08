"""真实 Fusion 输入落库后，验证分析、详情、图片、分页和超级管理员权限边界。"""
from collections import Counter
import json
import time
from urllib.error import HTTPError
from urllib.parse import urlencode
from urllib.request import Request


class RealtimeProbe:
    """按 ASP.NET Core 官方 SignalR JSON/LongPolling 协议验收真实服务，令牌只留内存。"""
    def __init__(self, api):
        self.api, self.sequence = api, 0
        negotiated = api.request('/hubs/sorting/negotiate?negotiateVersion=1', {}, 'POST')
        assert any(item['transport'] == 'LongPolling' for item in negotiated['availableTransports'])
        self.path = '/hubs/sorting?' + urlencode(dict(id=negotiated['connectionToken']))
        self.exchange('GET')
        self.exchange('POST', dict(protocol='json', version=1))
        assert any('error' not in item for item in self.poll()), 'SignalR 握手失败'

    def exchange(self, method, message=None):
        data = None if message is None else (json.dumps(message) + '\x1e').encode()
        request = Request(self.api.base + self.path, data=data, method=method,
                          headers={'Content-Type': 'text/plain;charset=UTF-8', 'X-Zeye-Client': 'web'})
        with self.api.http.open(request, timeout=40) as response:
            return response.read().decode()

    def poll(self):
        return [json.loads(item) for item in self.exchange('GET').split('\x1e') if item]

    def invoke(self, target, *arguments):
        self.sequence += 1
        invocation = str(self.sequence)
        self.exchange('POST', dict(type=1, invocationId=invocation, target=target, arguments=arguments))
        for _ in range(4):
            for item in self.poll():
                if item.get('invocationId') == invocation and item.get('type') == 3:
                    assert 'error' not in item, 'SignalR 调用异常：' + target
                    return item['result']
        raise AssertionError('SignalR 未返回调用结果：' + target)

    def close(self):
        self.exchange('DELETE')


def expect_status(api, path, status, body=None, method=None):
    """负面测试不记录认证 Cookie、密码及错误响应原文。"""
    data = None if body is None else json.dumps(body).encode()
    request = Request(api.base + path, data=data, method=method,
                      headers={'Content-Type': 'application/json', 'X-Zeye-Client': 'web'})
    try:
        with api.http.open(request, timeout=40) as response:
            actual = response.status
    except HTTPError as response:
        actual = response.code
        response.close()
    expected = (status,) if isinstance(status, int) else status
    assert actual in expected, f'{path}: 期望 HTTP {status}，实际 HTTP {actual}'


def verify_business_apis(fusion, hub, evidence):
    """按当前来源的真实入库总体对账；明细查询必须保留64位身份及多图关联。"""
    api = hub.api
    parcels = evidence['parcels']
    days = [item['createdTime'][:10] for item in parcels]
    scope = dict(fromDate=min(days), toDate=max(days), sourceInstanceId=fusion.source)
    timings = {}
    checks = []

    def read(path, parameters=None):
        started = time.perf_counter()
        result = api.request(path + ('?' + urlencode(parameters) if parameters else ''))
        timings[path + (':' + parameters.get('durationType', parameters.get('view', '')) if parameters else '')] = round((time.perf_counter() - started) * 1000, 2)
        return result

    listing = read('/api/parcels', dict(sourceInstanceId=fusion.source, pageNumber=1, pageSize=100, includeTotalCount='true'))
    assert listing['totalCount'] == len(parcels), '列表总体遗漏或混入其他来源'
    ids = [str(item['id']) for item in listing['items']]
    assert len(ids) == len(set(ids)) and set(ids).issubset(item['id'] for item in parcels)
    if len(parcels) > 100:
        next_page = read('/api/parcels', dict(sourceInstanceId=fusion.source, pageNumber=2, pageSize=100, includeTotalCount='true'))
        assert next_page['totalCount'] == len(parcels) and not set(ids).intersection(str(item['id']) for item in next_page['items'])
    checks.append('sourceScopedPaginationAndInt64Identity')

    for view in ('exceptions', 'chutes', 'duration'):
        result = read('/api/parcels/analysis', dict(**scope, view=view))
        assert result['parcelCount'] == len(parcels), '分析总体与入库总体不一致'
        assert result['completedCount'] == sum(item['status'] == 'Completed' for item in parcels)
        assert result['routingBlockedCount'] == sum(item['isRoutingBlocked'] for item in parcels)
    checks.append('analysisSnapshotTotals')
    duration_types = {}
    for kind in ('dws', 'routing', 'sorting', 'scan-upload', 'chute-request', 'landing-report', 'image-upload', 'other-api'):
        result = read('/api/parcels/analysis', dict(**scope, view='duration', durationType=kind, refreshDurationSnapshot='true'))
        duration = result['durationAnalysis']
        assert duration['type'] == kind and duration['observedCount'] == duration['sampleCount'] + duration['unavailableCount']
        assert sum(item['count'] for item in duration['buckets']) == duration['sampleCount'], '分布遗漏有效样本'
        assert len(duration['items']) <= 20 and duration['filteredCount'] == duration['sampleCount']
        if not duration['sampleCount']:
            assert all(duration[key] is None for key in ('averageMilliseconds', 'medianMilliseconds', 'p95Milliseconds'))
        else:
            assert 0 <= duration['minimumMilliseconds'] <= duration['medianMilliseconds'] <= duration['p95Milliseconds'] <= duration['maximumMilliseconds']
        if kind in ('dws', 'routing', 'sorting'):
            assert duration['observedCount'] == len(parcels)
        duration_types[kind] = dict(samples=duration['sampleCount'], unavailable=duration['unavailableCount'], failed=duration['failedCount'])
    checks.append('allDurationTypesAndDistribution')

    by_source_id = {item['sourceParcelId']: item for item in parcels}
    same = next(item for item in fusion.expected if item['scenario'] == 'same_barcode')
    candidates = read('/api/parcels/timing/candidates', dict(query=same['barcode'], searchBy='barcode'))
    repeated = sum(item['barCodes'] == same['barcode'] for item in parcels)
    assert candidates['totalCount'] == repeated > 1 and len(candidates['items']) == min(20, repeated)
    assert len({item['id'] for item in candidates['items']}) == len(candidates['items'])
    anchor = by_source_id[same['id']]['id']
    timing = read('/api/parcels/timing/' + anchor)
    assert timing['anchorId'] == anchor and 1 <= len(timing['items']) <= 11
    assert any(item['id'] == anchor for item in timing['items'])
    selected = [item['id'] for item in parcels[:3]]
    compared = read('/api/parcels/timing/compare', dict(ids=','.join(selected)))
    assert compared['requestedIds'] == selected and not compared['missingIds']
    assert [item['timing']['id'] for item in compared['items']] == selected
    checks.append('repeatedBarcodeAndTimingComparison')

    dws = read('/api/parcels/dws-consistency', dict(**scope, refresh='true'))
    assert dws['repeatedBarcodeCount'] >= 1 and dws['measurementCount'] >= repeated
    assert dws['scanTimingSampleCount'] + dws['missingScanTimingCount'] == dws['measurementCount']
    checks.append('dwsConsistency')
    counts = Counter(item['id'] for item in fusion.images)
    for source_id, count in counts.items():
        gallery = read('/api/parcels/' + by_source_id[source_id]['id'] + '/images')
        assert gallery['hasImages'] and len(gallery['images']) == count, '前端图片接口丢失多图关联'
        assert len({item['url'] for item in gallery['images']}) == count
    assert max(counts.values()) >= 3, '本轮没有同票三图案例'
    for scenario in ('normal', 'sorter_exception', 'late_dws'):
        item = next(item for item in fusion.expected if item['scenario'] == scenario)
        detail = read('/api/parcels/' + by_source_id[item['id']]['id'])
        assert str(detail['id']) == by_source_id[item['id']]['id']
    checks.append('parcelDetailsAndThreeImageGallery')
    read('/api/parcels/workbench')
    read('/api/parcels/processing-records/unbound', dict(limit=20))
    cursor = read('/api/parcels/cursor', dict(sourceInstanceId=fusion.source, pageSize=20))
    assert len(cursor['items']) == 20 and len({item['id'] for item in cursor['items']}) == 20
    checks.append('workbenchUnboundAndCursor')
    for path, status in (
        ('/api/parcels/timing/9223372036854775807', 404),
        ('/api/parcels/9223372036854775807', 404),
        ('/api/parcels/timing/compare?ids=1,2,3,4,5,6,7,8,9', 400),
        ('/api/parcels/analysis?' + urlencode(dict(**scope, view='duration', durationType='invalid')), 400),
        ('/api/parcels/analysis?view=duration&fromDate=invalid&toDate=2026-10-08', 400)):
        expect_status(api, path, status)
    checks.append('invalidInputAndMissingIdentity')
    return dict(checks=checks, durations=duration_types, galleryObjects=len(fusion.images),
                multiImageParcels=sum(count > 1 for count in counts.values()), httpMilliseconds=timings)


def verify_authorization(e2e_api_type, hub, source):
    """专属数据库验收环境中授予自定义角色全部权限，验证固定超级管理员边界。"""
    api = hub.api
    directory = api.request('/api/access')
    assert not any(item['account'].casefold() == 'hisoka' for item in directory['users'])
    role_name = '联合验收自定义全权限角色'
    role = next((item for item in directory['roles'] if item['name'] == role_name), None)
    body = dict(expectedRevision=directory['revision'], name=role_name, description='仅限隔离验收环境',
                permissions=[item['code'] for item in directory['permissions']])
    if role:
        body['id'] = role['id']
    directory = api.request('/api/access/roles', body)
    role = next(item for item in directory['roles'] if item['name'] == role_name)
    account = 'matrix-full-permissions'
    user = next((item for item in directory['users'] if item['account'] == account), None)
    body = dict(expectedRevision=directory['revision'], account=account, name='普通全权限验收用户',
                password='MatrixOrdinary20261008_ForTests', roleId=role['id'], enabled=True)
    if user:
        body['id'] = user['id']
    directory = api.request('/api/access/users', body)
    ordinary = e2e_api_type(api.base, cookie=True)
    anonymous = e2e_api_type(api.base, cookie=True)
    ordinary.request('/api/access/login', dict(username=account, password=body['password']))
    checks = []
    realtime = None
    try:
        assert not ordinary.request('/api/access/session')['isSuperAdministrator']
        ordinary.request('/api/parcels?pageNumber=1&pageSize=20&includeTotalCount=false')
        reads = ('/api/audit/web-requests?pageNumber=1&pageSize=20', '/api/diagnostics/slow-queries',
                 '/api/diagnostics/fusion/facts?' + urlencode(dict(sourceInstanceId=source, limit=20)),
                 '/api/data-governance/archive-tasks?pageNumber=1&pageSize=20', '/api/operations/partitions', '/api/admin/parcels/cleanup-history', '/health/deep')
        for path in reads:
            expect_status(ordinary, path, 403)
            expect_status(anonymous, path, 401)
            expect_status(api, path, (200, 503) if path == '/health/deep' else 200)
        writes = ('/api/admin/parcels', '/api/admin/parcels/batch-buffer', '/api/admin/parcels/processing-records',
                  '/api/admin/parcels/cleanup-expired', '/api/operations/partitions/prebuild', '/api/data-governance/archive-tasks')
        for path in writes:
            expect_status(ordinary, path, 403, {})
            expect_status(anonymous, path, 401, {})
        for identity in (ordinary, anonymous, api):
            expect_status(identity, '/hubs/fusion-ingestion/negotiate?negotiateVersion=1', 401, {}, 'POST')
        realtime = RealtimeProbe(ordinary)
        assert realtime.invoke('Read', '/api/parcels?pageNumber=1&pageSize=20&includeTotalCount=false')['statusCode'] == 200
        for path in reads:
            status = realtime.invoke('Read', path)['statusCode']
            unsupported = path.startswith('/api/diagnostics/fusion/facts?') or path == '/api/admin/parcels/cleanup-history'
            assert status == (400 if unsupported else 403), 'SignalR 未按白名单和身份阻断受限资源：' + path
        assert realtime.invoke('AppendProcessingRecord', '{}')['statusCode'] == 403
        latest = api.request('/api/access')
        expect_status(ordinary, '/api/access/users', 403,
                      dict(expectedRevision=latest['revision'], account='matrix-self-elevate', name='越权测试',
                           password='MatrixOrdinary20261008_ForTests', roleId=1))
        expect_status(api, '/api/access/users', 400,
                      dict(expectedRevision=latest['revision'], account='HiSoKa', name='保留名测试',
                           password='MatrixOrdinary20261008_ForTests', roleId=role['id']))
        checks.extend(('ordinaryReadAllowed', 'restrictedReadsForbidden', 'restrictedWritesForbidden',
                       'anonymousRejected', 'machineHubRejectsBrowserSessions', 'selfElevationRejected', 'reservedAccountHiddenAndCaseInsensitive',
                       'signalRReadAndWriteAuthorization'))
    finally:
        latest = api.request('/api/access')
        user = next(item for item in latest['users'] if item['account'] == account)
        api.request('/api/access/users', dict(expectedRevision=latest['revision'], id=user['id'],
                    account=account, name=user['name'], roleId=role['id'], enabled=False))
        if realtime:
            try:
                assert realtime.invoke('Read', '/api/parcels?pageNumber=1&pageSize=20')['statusCode'] == 401, '已有 SignalR 长连接保留停用账号权限'
            except HTTPError as response:
                assert response.code == 401, 'SignalR 传输未阻断已停用账号'
                response.close()
            try:
                realtime.close()
            except HTTPError as response:
                assert response.code in (401, 404)
                response.close()
        # 账号停用已撤销服务端会话，清除本进程 Cookie；空响应不交给 JSON API 解析器。
        for handler in ordinary.http.handlers:
            if hasattr(handler, 'cookiejar'):
                handler.cookiejar.clear()
    return dict(checks=checks, restrictedReadRoutes=len(reads), restrictedWriteRoutes=len(writes), testAccountDisabled=True)
