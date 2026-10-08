"""汇总本轮隔离联合验收的真实结果；任何缺项或失败都会阻止生成通过记录。"""
import argparse
import json
import re
from pathlib import Path
import xml.etree.ElementTree as ET


def read_json(path):
    """PowerShell 日志允许 BOM；只读取单条结构化工具输出。"""
    return json.loads(path.read_text(encoding='utf-8-sig'))


def summarize(root):
    """四库逐条对账、故障、业务写入、权限与启动迁移必须全部通过。"""
    providers = []
    for provider in ('mysql', 'sqlserver', 'oracle', 'sqlite'):
        candidates = sorted(root.glob('db-' + provider + '-*/result.json'))
        assert candidates, provider + ' 缺少联合验收结果'
        path = candidates[-1]
        result = read_json(path)
        verified = result['verified']
        scenarios = result['scenarios']
        names = set(scenarios) if isinstance(scenarios, dict) else {item['scenario'] for item in scenarios if item['phase'] == 'single'}
        assert result['provider'] == provider and len(names) == 19
        assert result['tickets'] == verified['parcels'] == (330 if provider == 'sqlite' else 360)
        assert all(verified[key] == 0 for key in ('missingFacts', 'duplicateParcels', 'pendingProjections'))
        assert verified['images'] == verified['imageContentsChecked'] == 20
        assert result['efVerification']['pending'] == 0 and result['fusionRestartPreservedIdentity']
        assert all(result['configuration'].values()) and result['authorization']['testAccountDisabled']
        assert result['connectionRejection']['rejected']
        assert len(result['businessApis']['checks']) == 8
        assert result['businessApis']['multiImageParcels'] == 1
        assert len(result['authorization']['checks']) == 8
        writes = result['businessWrites']
        assert len(writes['checks']) == 5 and writes['permanentSummaryOnly']
        assert writes['temporaryCrudParcelsRemoved'] == 4 and writes['cleanupFixtureParcelsRemoved'] == 2
        assert len(result['faults']) == (3 if provider == 'sqlite' else 4)
        assert all(item['recovered'] and item['identityPreserved'] and item['seconds'] <= 240 for item in result['faults'])
        protocol = read_json(root / (provider + '-protocol.json'))
        assert protocol['passed'] == len(protocol['checks']) == 28
        upgrade = read_json(root / (provider + '-startup-upgrade.json'))
        assert upgrade['startupUpgradeVerified'] and upgrade['durationIndexesVerified'] >= 3
        assert upgrade['dwsWindowIndexesVerified'] >= 3
        performance = read_json(root / (provider + '-performance-final.json'))
        queries = performance['queries']
        assert performance['count'] == 10000 and len(queries) == 22
        assert len({query['name'] for query in queries}) == 22
        assert all(len(query['steadyMilliseconds']) == 2 for query in queries)
        maximum_steady = max(value for query in queries for value in query['steadyMilliseconds'])
        # 与当前慢查询采集阈值一致；统计正确但存在秒级延迟不能汇总为性能通过。
        assert maximum_steady <= 500, provider + ' 存在超过 500 ms 的稳态查询'
        providers.append(dict(provider=provider, evidence=str(path.relative_to(root)),
                              tickets=result['tickets'], facts=verified['originalFacts'], images=verified['images'],
                              missingFacts=0, duplicateParcels=0, pendingProjections=0,
                              faults=result['faults'], protocolAssertions=28, startupUpgradeVerified=True,
                              businessReadGroups=8, businessWriteGroups=5, authorizationGroups=8,
                              configurationRestored=True, maximumReadHttpMilliseconds=max(result['businessApis']['httpMilliseconds'].values()),
                              performanceQueries=22, maximumSteadyMilliseconds=maximum_steady,
                              maximumFirstMilliseconds=max(query['firstMilliseconds'] for query in queries)))
    suites = []
    for kind in ('hub', 'fusion'):
        tree = ET.parse(root / (kind + '-tests') / (kind + '-regression.trx'))
        counters = next(item for item in tree.iter() if item.tag.endswith('Counters'))
        assert counters.attrib['failed'] == '0'
        assert counters.attrib['passed'] == counters.attrib['total']
        suites.append(dict(suite=kind, passed=int(counters.attrib['passed'])))
    frontend = (root / 'frontend-tests-final.log').read_text(encoding='utf-8-sig')
    totals = {key: int(re.search(r'\b' + key + r' (\d+)\s*$', frontend, re.MULTILINE)[1])
              for key in ('tests', 'pass', 'fail', 'skipped', 'cancelled')}
    assert totals['tests'] == totals['pass'] and all(totals[key] == 0 for key in ('fail', 'skipped', 'cancelled'))
    browser = read_json(root / 'browser-final.json')
    assert len(browser['routes']) == 28 and len(browser['durations']) == 9 and not browser['failures']
    assert all(item['loaded'] for item in browser['durations'])
    summary = dict(providers=providers, backendSuites=suites, frontendPassed=totals['pass'],
                   browserPages=28, browserDurationTypes=9, allProviderChecksPassed=True)
    (root / 'joint-summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
    return summary


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('evidence_root', type=Path)
    arguments = parser.parse_args()
    print(json.dumps(summarize(arguments.evidence_root.resolve()), ensure_ascii=False))
