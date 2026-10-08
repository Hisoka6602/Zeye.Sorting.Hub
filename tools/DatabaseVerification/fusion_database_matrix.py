"""复用 Fusion 的真实 TCP 设备验收流程，逐一验证隔离的四数据库 Hub。"""
import argparse
import http.client
from collections import Counter
from contextlib import closing
from datetime import datetime
from decimal import Decimal
import hashlib
import importlib
import json
from pathlib import Path
import re
import subprocess
import sqlite3
import sys
import threading
import time
import urllib.error
import urllib.request
from types import SimpleNamespace
from database_fault_scenarios import DatabaseFaultScenarios
from joint_business_assertions import verify_business_apis, verify_authorization
from joint_write_assertions import verify_business_writes


def verify_connection_rejection(provider, service, directory, state):
    """临时进程使用错误连接，宿主有效配置保持原值，并把预期驱动错误保存为证据。"""
    names = dict(mysql='MySql', sqlserver='SqlServer', oracle='Oracle', sqlite='SQLite')
    key = 'ConnectionStrings__' + names[provider]
    expected = dict(mysql='Access denied for user', sqlserver='Login failed for user',
                    oracle='ORA-01017', sqlite='unable to open database file')[provider]
    if provider == 'sqlite':
        missing = '/app/data/connection-faults/' + directory.name + '/data/business/nested/sorting-hub.db'
        connection = 'Data Source=' + missing + ';Mode=ReadOnly;Default Timeout=3;'
    else:
        connection = next(item.split('=', 1)[1] for item in state['Config']['Env'] if item.startswith(key + '='))
        connection, changed = re.subn(r'(?i)(password|pwd)\s*=[^;]*;', r'\1=WrongPassword_ForNegativeVerification;', connection, count=1)
        assert changed == 1, '未找到隔离业务库连接的密码字段'
        if provider == 'mysql':
            # caching_sha2_password 的错误密码会进入完整认证，使用测试服务器已有 TLS 到达凭据检查。
            # 宿主业务连接保持原值，不启用不受信任的 RSA 公钥获取。
            connection, changed = re.subn(r'(?i)ssl\s*mode\s*=[^;]*;', 'SslMode=Required;', connection, count=1)
            if not changed:
                connection += 'SslMode=Required;'
    process = subprocess.run(['docker', 'exec', '-e', key + '=' + connection, service,
        'dotnet', '/app/verification/DatabaseVerification.dll', '--probe-connection'], capture_output=True, text=True,
        encoding='utf-8', errors='replace', timeout=90,
        creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
    output = process.stdout + process.stderr
    (directory / 'connection-rejection.log').write_text(output, encoding='utf-8')
    assert process.returncode != 0 and expected in output, '错误连接未返回预期驱动拒绝：' + output[:1200] + output[-500:]
    result = dict(scenario='missing_readonly_file' if provider == 'sqlite' else 'wrong_database_password',
                  rejected=True, expectedError=expected, exitCode=process.returncode)
    if provider == 'mysql':
        result['authenticationTransport'] = 'TLSRequired'
    if provider == 'sqlite':
        probe = subprocess.run(['docker', 'exec', service, 'test', '-e', str(Path(missing).parent).replace('\\', '/')], timeout=15)
        assert probe.returncode == 1, '只读连接创建了缺失文件或目录'
        result['missingDirectoryNotCreated'] = True
    (directory / 'connection-rejection.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    return result


def verify_evidence(fusion, hub, evidence):
    """逐条对照 Fusion 发送库原文、设备期望和图片内容，计数不能代替业务语义核对。"""
    outbox = fusion.directory / 'data/hub-outbox/outbox.db'
    with closing(sqlite3.connect(f'{outbox.as_uri()}?mode=ro', uri=True)) as database:
        sent = database.execute('SELECT RecordId,Sequence,BodySha256,State,BodyJson FROM Facts WHERE Sequence>?', (fusion.baseline_sequence,)).fetchall()
    received = {item['recordId']: item for item in evidence['receipts']}
    assert len(received) == len(sent), 'Fusion 与 Hub 原始事实数量不一致'
    by_parcel = {}
    for identity, sequence, sha, state, body in sent:
        fact = received[identity]
        assert state == 'acknowledged' and fact['sequence'] == str(sequence)
        assert fact['bodyJson'] == body and fact['bodySha256'] == sha == hashlib.sha256(body.encode()).hexdigest()
        assert fact['projectionState'] == 'complete' and not fact['projectionError']
        payload = json.loads(body)
        if payload['sourceParcelId'] is not None:
            by_parcel.setdefault((payload['sourceRunId'], str(payload['sourceParcelId'])), []).append(payload)
    parcels = {(item['sourceRunId'], item['sourceParcelId']): item for item in evidence['parcels']}
    assert len(parcels) == len(evidence['parcels']) == len(fusion.expected), '包裹身份重复或丢失'
    for expected in fusion.expected:
        parcel = parcels[expected['run'], expected['id']]
        proof = by_parcel[expected['run'], expected['id']]
        assert any(item['kind'] == 'parcel.detected' for item in proof), '缺少原始检测事实'
        if expected['scenario'] == 'sorter_exception':
            assert parcel['isRoutingBlocked'] and parcel['sourceExceptionCode'] in ('TargetChuteAssignmentRejected', 'ProcessInterrupted')
            assert not any(item['kind'] in ('parcel.dispatched', 'parcel.landed') for item in proof), '分拣机拒绝后仍执行分拣'
            continue
        assert parcel['barCodes'] == (expected['barcode'] or '')
        assert int(parcel['actualChuteCode']) == expected['actual'] and int(parcel['targetChuteCode']) == expected['target']
        assert sum(item['kind'] == 'parcel.dispatched' for item in proof) == 1
        assert sum(item['kind'] == 'parcel.landed' for item in proof) == 1
        if expected['weight'] is not None:
            assert Decimal(str(parcel['weight'])) * 1000 == expected['weight']
            assert (parcel['length'], parcel['width'], parcel['height']) == (300, 200, 100)
            decisions = [item['data'] for item in proof if item['kind'] == 'provider.decision']
            assert len(decisions) == 1 and decisions[0]['barcode'] == expected['barcode']
            success = expected['scenario'] not in ('scan_rejected', 'http_failure', 'invalid_response', 'provider_timeout', 'noread')
            assert decisions[0]['isAssigned'] == success, '传输成功与业务接受混淆'
        if expected['scenario'] in ('dws_timeout', 'late_dws'):
            assert parcel['isFallbackChuteAssigned'] and parcel['sourceExceptionCode'] == 'DwsTimeout'
        if expected['scenario'] == 'http_failure':
            assert any(item['kind'] == 'provider.interaction' and item['data'].get('statusCode') == 503 for item in proof)
    assert len(evidence['images']) == len(fusion.images), '图片对象数量不一致'
    remaining = list(evidence['images'])
    for expected in fusion.images:
        matches = [item for item in remaining if item['sourceParcelId'] == expected['id'] and item['isStored']
                   and item['contentSha256'] == expected['sha']]
        assert matches, '图片对象缺失、关联错误或内容损坏'
        image = matches[0]
        remaining.remove(image)
        content = hub.api.bytes('/api/parcels/fusion/images/' + image['key'] + '/content')
        assert len(content) == image['sizeBytes'] and hashlib.sha256(content).hexdigest() == expected['sha']
        assert parcels[image['sourceRunId'], expected['id']]['hasImages']
    assert not remaining and len({item['key'] for item in evidence['images']}) == len(fusion.images)
    return dict(parcels=len(parcels), originalFacts=len(sent), images=len(evidence['images']),
                imageContentsChecked=len(fusion.images), factsByKind=dict(Counter(item['kind'] for item in evidence['receipts'])),
                missingFacts=0, duplicateParcels=0, pendingProjections=0)


def verify_configuration(hub):
    """验证管理员原值读取、热更新和过期版本冲突，结束时恢复原配置值。"""
    path = '/api/operations/configuration/runtime'
    state = hub.api.request(path)
    key = 'LogCleanup:RetentionDays'
    original = state['configuration']['LogCleanup']['RetentionDays']
    changed = original + 1 if original < 365 else original - 1
    assert key in state['hotReloadKeys'] and 'Persistence:Provider' not in state['hotReloadKeys']
    saved = hub.api.request(path, dict(revision=state['revision'], changes=dict(LogCleanup=dict(RetentionDays=changed))), 'PUT')
    try:
        assert int(saved['snapshot']['effectiveConfiguration'][key]) == changed
        assert key not in saved['result']['restartRequiredKeys']
        try:
            hub.api.request(path, dict(revision=state['revision'], changes=dict(LogCleanup=dict(RetentionDays=original))), 'PUT')
        except AssertionError as failure:
            assert 'HTTP 409' in str(failure)
        else:
            raise AssertionError('过期配置版本未阻断保存')
    finally:
        latest = hub.api.request(path)
        restored = hub.api.request(path, dict(revision=latest['revision'], changes=dict(LogCleanup=dict(RetentionDays=original))), 'PUT')
        assert int(restored['snapshot']['effectiveConfiguration'][key]) == original
    history = hub.api.request('/api/operations/configuration/history?limit=20')
    committed = next(item for item in history if item['revision'] == saved['result']['revision'])
    assert committed['status'] == 'Committed'
    assert json.loads(committed['beforeJson'])['LogCleanup']['RetentionDays'] == original
    assert json.loads(committed['afterJson'])['LogCleanup']['RetentionDays'] == changed
    return dict(adminOriginalValues=True, hotReloadEffective=True, staleVersionRejected=True, originalValueRestored=True, historyPersisted=True)


def collect_result(docker, service, fusion, hub, directory, provider, metadata=None):
    """通过数据库和实际 API 复核本轮设备输入，保留可重复验收的完整结果。"""
    rows = hub.api.request('/api/parcels?pageSize=100&pageNumber=1&includeTotalCount=false')
    verification = docker('exec', service, 'dotnet', '/app/verification/DatabaseVerification.dll', '--source', fusion.source, timeout=300)
    docker('cp', service + ':/app/data/business-history/verification-evidence.json', str(directory / 'hub-evidence.json'))
    evidence = json.loads((directory / 'hub-evidence.json').read_text(encoding='utf-8'))
    verified = verify_evidence(fusion, hub, evidence)
    local_day = datetime.now().strftime('%Y-%m-%d')
    hub.api.request('/api/parcels/analytics?fromDate=' + local_day + '&toDate=' + local_day)
    hub.api.request('/api/parcels/processing-records/unbound?limit=50')
    hub.api.request('/api/parcels/cursor?pageSize=20')
    hub.api.request('/api/parcels/workbench')
    result = dict(provider=provider, tickets=len(fusion.expected), scenarios=fusion.scenarios,
                  fusionRestartPreservedIdentity=len({item['run'] for item in fusion.expected}) == 1,
                  efVerification=json.loads(verification), verified=verified, apiSmoke=bool(rows), configuration=verify_configuration(hub),
                  businessApis=verify_business_apis(fusion, hub, evidence), authorization=verify_authorization(e2e_api_type=type(hub.api), hub=hub, source=fusion.source),
                  businessWrites=verify_business_writes(hub.api, docker, service))
    if metadata:
        result.update(metadata)
    (directory / 'result.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(result, ensure_ascii=False), flush=True)
    return result


def main():
    """只有专属 Docker 验收项目可以被此入口操作；结果保存在工作区测试目录。"""
    parser = argparse.ArgumentParser()
    parser.add_argument('--fusion-root', default=r'D:\WorkSpace\Zeye\Zeye.SortingFusionService')
    parser.add_argument('--provider', choices=['mysql', 'sqlserver', 'oracle', 'sqlite'], required=True)
    parser.add_argument('--count', type=int, default=50)
    parser.add_argument('--batch-size', type=int, default=10, help='每批设备票数，范围 1..100')
    parser.add_argument('--extended-scenarios', action='store_true', help='增加超时、重试、分片和部分测量场景')
    parser.add_argument('--faults', action='store_true', help='执行数据库及 Hub/Fusion 中断恢复验收')
    parser.add_argument('--output', required=True)
    parser.add_argument('--verify-existing', help='重新对账已保留的本轮验收目录，不重新发送设备业务')
    args = parser.parse_args()
    if not 1 <= args.batch_size <= 100 or not 20 <= args.count <= 10000:
        parser.error('batch-size 范围 1..100，count 范围 20..10000')
    sys.path.insert(0, str(Path(args.fusion_root) / 'tools'))
    e2e = importlib.import_module('fusion_hub_load_e2e')
    utilities = importlib.import_module('unattended_fixtures')
    original_docker = utilities.docker
    service = 'zeye-db-verification-hub-' + args.provider + '-1'
    state = json.loads(original_docker('inspect', service))[0]
    assert state['Config']['Labels']['com.docker.compose.project'] == 'zeye-db-verification'
    network = 'zeye-db-verification_default'
    discovery_target = state['NetworkSettings']['Networks'][network]['IPAddress']
    tag = 'db-' + args.provider + '-' + str(int(time.time()))
    directory = Path(args.verify_existing).resolve() if args.verify_existing else Path(args.output).resolve() / tag
    assert directory.is_relative_to(Path(args.output).resolve())
    if not args.verify_existing:
        directory.mkdir(parents=True, exist_ok=False)
    ports = dict(mysql=5191, sqlserver=5192, oracle=5193, sqlite=5194)
    repository = Path(__file__).resolve().parents[2]

    # 容器 Running 不表示迁移和 HTTP 入口就绪；启动等待不发送或修改业务数据。
    deadline = time.monotonic() + 120
    while True:
        try:
            with urllib.request.urlopen('http://127.0.0.1:' + str(ports[args.provider]) + '/health/ready', timeout=3) as response:
                assert response.status == 200
            break
        except (OSError, http.client.HTTPException, urllib.error.URLError):
            if time.monotonic() >= deadline:
                raise AssertionError('隔离 Hub 在 120 秒内未通过 HTTP 与数据库就绪检查')
            time.sleep(1)

    class MatrixHub(e2e.Hub):
        """复用管理员配置接口，通过 EF 工具复核数据，避免依赖 MySQL 查询。"""
        def __init__(self):
            self.api = e2e.Api('http://127.0.0.1:' + str(ports[args.provider]), cookie=True)
            session = self.api.request('/api/access/session')
            credentials = dict(username='matrix-admin', password='MatrixAdministrator20261007_ForTests')
            if not session['configured']:
                self.api.request('/api/access/bootstrap', dict(**credentials, name='数据库验收管理员', bootstrapKey='MatrixBootstrap20261007_ForTests'))
            self.api.request('/api/access/login', credentials)
            self.path = '/api/operations/configuration/fusion'
            self.lock = threading.Lock()
            self.owned = {}
            settings = self.api.request(self.path)
            assert settings['settings']['isEnabled'] and settings['settings']['discoveryEnabled']
            self.hub_id = settings['hubId']
            self.endpoint = settings['settings']['advertisedEndpoint']
            self.port = settings['settings']['discoveryPort']

    hub = MatrixHub()

    if args.verify_existing:
        # 复验只更新对账与配置实测，保留原轮次已完成的故障和连接拒绝证据。
        result_path = directory / 'result.json'
        previous_result = json.loads(result_path.read_text(encoding='utf-8')) if result_path.exists() else {}
        evidence = next(directory.glob('py-e2e-db-' + args.provider + '-*/expected.json'))
        existing = SimpleNamespace(directory=evidence.parent, source=evidence.parent.name, baseline_sequence=0,
            expected=json.loads(evidence.read_text(encoding='utf-8')),
            images=json.loads((evidence.parent / 'expected-images.json').read_text(encoding='utf-8')))
        existing.scenarios = dict(Counter(item['scenario'] for item in existing.expected))
        preserved = {key: previous_result[key] for key in ('faults', 'connectionRejection', 'scenarios') if key in previous_result}
        for key, filename in (('faults', 'faults/results.json'), ('connectionRejection', 'connection-rejection.json')):
            if key not in preserved and (directory / filename).exists():
                preserved[key] = json.loads((directory / filename).read_text(encoding='utf-8'))
        return collect_result(original_docker, service, existing, hub, directory, args.provider, preserved)

    def docker(*arguments, **keywords):
        """只为本轮设备容器加入独立网络，所有清理仍限制在本轮专属名字。"""
        if arguments and arguments[0] == 'run':
            assert '--name' in arguments
            name = arguments[arguments.index('--name') + 1]
            assert name.startswith('zsf-py-e2e-' + tag)
            arguments = (*arguments[:-1], '--network', network, arguments[-1])
        return original_docker(*arguments, **keywords)

    e2e.docker = docker
    utilities.docker = docker
    e2e.PLAN = [('normal', 2300), ('same_barcode', 500), ('out_of_order', 500), ('duplicate_frames', 500),
                ('http_failure', 300), ('chute_mismatch', 300), ('protocol_noise', 200), ('hub_offline', 200), ('noread', 200)]
    if args.extended_scenarios:
        e2e.PLAN = [('normal', 800), ('same_barcode', 400), ('out_of_order', 400), ('duplicate_frames', 400),
                    ('http_failure', 250), ('chute_mismatch', 200), ('protocol_noise', 100), ('hub_offline', 100),
                    ('noread', 150), ('fragmented', 200), ('retry_recovery', 300), ('provider_timeout', 200),
                    ('invalid_response', 200), ('partial_completed', 200), ('scan_rejected', 250),
                    ('fifo_binding', 250), ('dws_timeout', 300), ('late_dws', 150), ('sorter_exception', 150)]
    assert sum(weight for _, weight in e2e.PLAN) == 5000
    reserved = set()

    def reserve(parcel_id):
        """确保测试设备的长整数编号不会重用。"""
        assert parcel_id not in reserved
        reserved.add(parcel_id)

    run = SimpleNamespace(hub=hub, tag=tag, directory=directory, args=SimpleNamespace(image='zeye-sortingfusion:local',
        discovery_target=discovery_target, batch=args.batch_size, drain_timeout=240), reserve_device_id=reserve,
        device_id_base=639270000000000000 + ports[args.provider] * 1000000, unique_device_ids=True)
    fusion = e2e.Fusion(run, 1)
    result = {}
    try:
        connection_rejection = verify_connection_rejection(args.provider, service, directory, state)
        fusion.start()
        # 独立测试工作台启用可识别多相机文件名，主 Fusion 配置保持原值。
        fusion.save({'ImageMonitoring': {'FileNameMode': 'Regex',
                    'FileNamePattern': r'^(?<barcode>.+?)(?:__CAM_(?<camera>\d+))?$'}})
        publish = fusion.publish_image
        multi_image_created = False

        def publish_multiple(filename, content):
            nonlocal multi_image_created
            publish(filename, content)
            if multi_image_created:
                return
            expected = next(item for item in fusion.expected if item['barcode'] == filename[:-4])
            multi_image_created = True
            for camera in (2, 3):
                publish(filename[:-4] + '__CAM_' + str(camera) + '.jpg', content)
                fusion.images.append(dict(id=expected['id'], barcode=expected['barcode'], sha=hashlib.sha256(content).hexdigest()))

        fusion.publish_image = publish_multiple
        fusion.phase('single', args.count)
        faults = DatabaseFaultScenarios(original_docker, args.provider, service, fusion, utilities.wait_for).run() if args.faults else []
        if not args.faults:
            first = next(item for item in fusion.expected if item['scenario'] == 'normal')
            fusion.publish_image(first['barcode'] + '.jpg', utilities.JPEG)
        fusion.drain()
        fusion.restart()
        fusion.batch('multi', 'normal', 10)
        fusion.drain()
        result = collect_result(original_docker, service, fusion, hub, directory, args.provider,
                                dict(faults=faults, connectionRejection=connection_rejection))
    except Exception as failure:
        (directory / 'failure.json').write_text(json.dumps(dict(type=type(failure).__name__, message=str(failure)), ensure_ascii=False), encoding='utf-8')
        if fusion.started:
            (directory / 'fusion-docker.log').write_text(original_docker('logs', '--tail', '800', fusion.name), encoding='utf-8')
            (directory / 'hub-docker.log').write_text(original_docker('logs', '--tail', '800', service), encoding='utf-8')
            try:
                (directory / 'last-status.json').write_text(json.dumps(fusion.status(), ensure_ascii=False, indent=2), encoding='utf-8')
            except OSError:
                pass
            original_docker('stop', '--timeout', '60', fusion.name)
            fusion.export_databases()
        raise
    finally:
        if fusion.dws:
            fusion.dws.close()
        if fusion.started:
            original_docker('rm', '-f', fusion.name)
        if fusion.volume_created:
            original_docker('volume', 'rm', fusion.volume)
        fusion.provider.close()
        fusion.sorter.close()
    return result


if __name__ == '__main__':
    main()
