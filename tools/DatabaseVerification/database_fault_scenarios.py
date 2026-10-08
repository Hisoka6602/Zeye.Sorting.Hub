"""在专属 Docker 项目制造可恢复故障，以真实 Fusion 发送库证明离线积压及恢复。"""
from concurrent.futures import ThreadPoolExecutor
import json
import subprocess
import time


class DatabaseFaultScenarios:
    """所有故障只操作已检查归属的验收容器，异常退出也恢复网络和宿主。"""

    def __init__(self, docker, provider, service, fusion, wait_for):
        """绑定本轮来源和测试容器；证据不会覆盖其他轮次。"""
        # Docker 调用、容器身份、真实 Fusion 和等待函数均来自本轮隔离运行。
        self.docker, self.provider, self.service = docker, provider, service
        self.fusion, self.wait_for = fusion, wait_for
        # 独立项目名及网络，服务器型提供器有额外数据库容器。
        self.network = 'zeye-db-verification_default'
        self.server = None if provider == 'sqlite' else 'zeye-db-verification-' + provider + '-1'
        # 分步证据与持锁进程；失败时保留已完成场景和异常记录。
        self.results, self.lock_process = [], None
        self.directory = fusion.directory.parent / 'faults'
        self.directory.mkdir(exist_ok=True)
        for container in (service, self.server):
            if container:
                state = json.loads(docker('inspect', container))[0]
                assert state['Config']['Labels']['com.docker.compose.project'] == 'zeye-db-verification'
                assert state['State']['Running'] and self.network in state['NetworkSettings']['Networks']
        assert json.loads(docker('inspect', fusion.name))[0]['Config']['Labels']['zsf.hub-e2e.run'] == fusion.run.tag

    def write(self, name, value):
        """保存本地时间语义下的业务状态和每步验收结果。"""
        (self.directory / (name + '.json')).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')

    def recovered(self):
        """等待真实链路重连、积压完全确认，且发送库没有丢弃或拒绝事实。"""
        deadline, next_notice, status = time.monotonic() + 240, 0, None
        while time.monotonic() < deadline:
            try:
                status = self.fusion.status()
            except OSError:
                time.sleep(1)
                continue
            journal = status['journal']
            assert journal['rejectedFacts'] == 0 and journal['droppedUnacknowledgedFacts'] == 0
            assert journal['droppedUnacknowledgedImages'] == 0
            if status['connected'] and journal['pendingFacts'] == 0 and journal['pendingImages'] == 0:
                return status
            if time.monotonic() >= next_notice:
                self.write('recovery-latest', status)
                print(f'RECOVERY {self.provider} connected={status["connected"]} facts={journal["pendingFacts"]} images={journal["pendingImages"]} error={status["lastErrorCode"]}', flush=True)
                next_notice = time.monotonic() + 15
            # 等待租约会持续两分钟；状态接口落盘日志，长等待采用每秒一次的采样。
            time.sleep(1)
        self.write('recovery-timeout', status)
        raise AssertionError('database fault recovery and durable ACKs exceeded 240 seconds')

    def backlog(self, name):
        """故障必须留下待确认原始事实，避免只有故障命令成功的假验收。"""
        status = self.wait_for(lambda: self.fusion.status() if self.fusion.status()['journal']['pendingFacts'] > 0 else None,
                               'durable backlog during ' + name, 25)
        self.write(name + '-backlog', status)
        return status['journal']['pendingFacts']

    def load(self, name, count=30):
        """设备继续分拣并写入 Fusion 本地历史，Hub 故障不影响已有物理业务。"""
        baseline = len(self.fusion.expected)
        self.fusion.batch('multi', 'normal', count)
        tickets = self.fusion.expected[baseline:]
        deadline = time.monotonic() + 30
        while time.monotonic() < deadline:
            rows = self.fusion.rows()
            if all(rows.get(item['id'], {}).get('completedAtMs') for item in tickets):
                break
            time.sleep(0.5)
        else:
            raise AssertionError('真实落格尚未提交 Fusion 本地历史，不能进行发送库崩溃重放验收')
        self.write(name + '-local-commit', dict(tickets=[item['id'] for item in tickets], allLandingsDurable=True))
        self.fusion.scenarios.append(dict(phase='multi', scenario=name, tickets=count))

    def record(self, name, started, pending, **details):
        """记录恢复耗时、积压证据和复用的来源身份，最终另做逐条数据库对账。"""
        recovered = self.recovered()
        assert (self.fusion.run_id, self.fusion.journal_id) == (recovered['journal']['sourceRunId'], recovered['journal']['journalId'])
        result = dict(scenario=name, recovered=True, pendingFactsDuringFault=pending,
                      seconds=round(time.monotonic() - started, 3), identityPreserved=True, **details)
        self.results.append(result)
        self.write('results', self.results)
        self.write(name + '-recovered', recovered)
        print('FAULT PASS ' + self.provider + ' ' + json.dumps(result, ensure_ascii=False), flush=True)

    def network_partition(self):
        """断开数据库网络并占用旧地址，保留进程和数据卷，验证服务名在新地址恢复。"""
        self.recovered()
        state = json.loads(self.docker('inspect', self.server))[0]
        address = state['NetworkSettings']['Networks'][self.network]['IPAddress']
        reservation = 'zeye-db-verification-address-' + self.fusion.run.tag
        started = time.monotonic()
        self.docker('network', 'disconnect', self.network, self.server)
        try:
            # 使用已有镜像的休眠进程暂占旧地址，确保故障实际覆盖地址变化，无外部服务。
            self.docker('run', '-d', '--name', reservation, '--network', self.network, '--ip', address,
                        '--label', 'zsf.hub-e2e.run=' + self.fusion.run.tag,
                        '--entrypoint', '/bin/sleep', 'zeye-sorting-hub:database-verification', '300')
            self.load('database_network_partition')
            pending = self.backlog('database_network_partition')
        finally:
            try:
                # 数据库始终使用动态地址，避免静态 IP 与后续 Docker 重启的 IPAM 状态竞争。
                self.docker('network', 'connect', '--alias', self.provider, self.network, self.server)
            finally:
                reserved = subprocess.run(['docker', 'inspect', reservation], capture_output=True, text=True)
                if reserved.returncode == 0:
                    assert json.loads(reserved.stdout)[0]['Config']['Labels']['zsf.hub-e2e.run'] == self.fusion.run.tag
                    self.docker('rm', '-f', reservation)
        restored = json.loads(self.docker('inspect', self.server))[0]['NetworkSettings']['Networks'][self.network]['IPAddress']
        assert restored != address, '数据库网络故障必须覆盖地址变化，不能使用原地址形成假验收'
        self.record('database_network_partition', started, pending, previousAddress=address, restoredAddress=restored, addressChanged=True)

    def server_crash(self):
        """强制结束隔离数据库进程，卷保留，恢复后验证连接池和真实重放。"""
        self.recovered()
        started = time.monotonic()
        self.docker('kill', self.server)
        try:
            self.load('database_crash_recovery')
            pending = self.backlog('database_crash_recovery')
        finally:
            self.docker('start', self.server)
        self.record('database_crash_recovery', started, pending)

    def hub_crash_in_flight(self):
        """在真实设备批次仍运行且事实序号已增长时强制结束 Hub。"""
        baseline = self.recovered()['journal']['lastSequence']
        started = time.monotonic()
        with ThreadPoolExecutor(max_workers=1) as workers:
            loading = workers.submit(self.load, 'hub_crash_in_flight', 60)
            self.wait_for(lambda: self.fusion.status()['journal']['lastSequence'] > baseline + 5,
                          'in-flight Fusion facts before Hub crash', 30)
            in_flight = not loading.done()
            assert in_flight, '设备批次已完成，不能把普通停机误称为处理中断'
            self.docker('kill', self.service)
            try:
                loading.result(timeout=120)
                pending = self.backlog('hub_crash_in_flight')
            finally:
                self.docker('start', self.service)
        self.record('hub_crash_in_flight', started, pending, inFlightLoad=in_flight)

    def sqlite_lock_timeout(self):
        """SQLite 持锁超过命令超时，验证 busy 错误不会永久拒绝真实业务。"""
        self.recovered()
        started = time.monotonic()
        self.lock_process = subprocess.Popen(['docker', 'exec', self.service, 'dotnet',
            '/app/verification/DatabaseVerification.dll', '--hold-write-lock', '40'],
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding='utf-8',
            creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
        lines = []
        try:
            for line in self.lock_process.stdout:
                lines.append(line)
                if 'SQLITE_WRITE_LOCK_READY' in line:
                    break
            assert any('SQLITE_WRITE_LOCK_READY' in line for line in lines), 'SQLite 未取得写锁'
            self.load('sqlite_busy_timeout')
            pending = self.backlog('sqlite_busy_timeout')
            remaining, _ = self.lock_process.communicate(timeout=65)
            lines.append(remaining)
            assert self.lock_process.returncode == 0 and 'SQLITE_WRITE_LOCK_RELEASED' in remaining
        finally:
            (self.directory / 'sqlite-write-lock.log').write_text(''.join(lines), encoding='utf-8')
            if self.lock_process.poll() is None:
                # 持锁进程有 60 秒硬上限，正常等待使事务明确回滚，不删除 SQLite 文件。
                self.lock_process.wait(timeout=65)
        self.record('sqlite_busy_timeout', started, pending, lockSeconds=40)

    def fusion_crash_with_backlog(self):
        """待确认事实已耐久写入时结束 Fusion，重新启动同一卷验证不丢票。"""
        self.recovered()
        started = time.monotonic()
        self.docker('kill', self.service)
        try:
            self.load('fusion_crash_with_backlog')
            pending = self.backlog('fusion_crash_with_backlog')
            self.docker('kill', self.fusion.name)
        finally:
            self.docker('start', self.service)
        # 两端强制中断不会正常释放数据库租约，先覆盖租约到期后的安全重连。
        # 引用工具的普通启动等待为 40 秒，不适用于默认 120 秒租约的崩溃场景。
        self.docker('start', self.fusion.name)
        self.wait_for(self.fusion.healthy, 'crashed Fusion healthy', 60)
        self.recovered()
        self.fusion.restart()
        self.record('fusion_crash_with_backlog', started, pending, durableOutboxAfterCrash=True)

    def run(self):
        """执行分库故障和两个进程中断场景，异常时保留证据并恢复专属容器。"""
        self.fusion.save({'DataFusion': dict(Timeout=10000, WcsRequestTimeoutMilliseconds=3000,
                          WcsMaxRetryAttempts=0, RequireExactCorrelation=True, UseFifoBindingWindow=False,
                          MeasurementRequirement='BarcodeOnly')})
        try:
            if self.server:
                self.network_partition()
                self.server_crash()
            else:
                self.sqlite_lock_timeout()
            self.hub_crash_in_flight()
            self.fusion_crash_with_backlog()
        except Exception as failure:
            self.write('failure', dict(type=type(failure).__name__, message=str(failure), completed=self.results))
            raise
        finally:
            for container in (self.service, self.server):
                if container:
                    state = json.loads(self.docker('inspect', container))[0]
                    if self.network not in state['NetworkSettings']['Networks']:
                        self.docker('network', 'connect', '--alias', self.provider, self.network, container)
                    if not state['State']['Running']:
                        self.docker('start', container)
        return self.results
