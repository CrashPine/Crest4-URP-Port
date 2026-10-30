"""Validate the bounded ownership matrix without rerunning Unity. Python stdlib only."""
import datetime
import hashlib
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parent.parent
PREFIX = sys.argv[1] if len(sys.argv) > 1 else 'P10-Ownership'
if not re.fullmatch(r'[A-Za-z0-9_-]+', PREFIX):
    raise ValueError('Invalid evidence prefix')
EVIDENCE = ROOT / 'Evidence'
runs = json.loads((EVIDENCE / f'{PREFIX}-runs.json').read_text(encoding='utf-8-sig'))
failures, cases = [], []


def require(condition, description):
    if not condition:
        failures.append(description)


def temps(stage):
    return [t for t in stage['snapshot']['textures'] if 'TempBuffer' in t['name']]


require(len(runs) == 10, 'Expected ten cases')
for run in runs:
    name = run['name']
    require(run['exitCode'] == 0 and not run['timedOut'], name + ': process failure')
    path = EVIDENCE / (name + '.json')
    if not path.exists():
        require(False, name + ': missing report')
        continue
    report = json.loads(path.read_text())
    require(report['errors'] == 0, name + ': runtime errors')
    require(report['unity'] == '6000.6.5f1', name + ': wrong Unity')
    require(report['api'] == ('Direct3D11' if name.endswith('DX11') else 'Direct3D12'), name + ': wrong API')
    log = (EVIDENCE / (name + '.log')).read_text(errors='replace')
    require('OWNERSHIP_PROBE_COMPLETED' in log, name + ': missing completion marker')
    warnings = re.findall(r'^.*(?:Leak Detected|unreleased.*evictable|Exception:|Assertion failed).*$' , log, re.M | re.I)
    stages = report['stages']
    for stage in stages:
        driver = stage['driverMemory']
        require(driver['status'] == 'ok' and driver['localHresult'] == driver['nonLocalHresult'] == 0,
                name + '/' + stage['stage'] + ': invalid driver query')
        require(driver['vendor'] == 0x10DE and driver['device'] == 0x25A0,
                name + ': unexpected adapter for this recorded host')
    cycles = []
    for cycle in range(1 if report['mode'] == 'Empty' else 3):
        subset = {s['stage'].split('-', 1)[1]: s for s in stages if s['stage'].startswith(f'cycle{cycle}-')}
        require(not temps(subset['setup']), name + ': unexpected setup TempBuffer')
        require(bool(temps(subset['submitted'])) == report['hdr'], name + ': HDR control mismatch')
        require(not temps(subset['held']), name + ': TempBuffer still visible after hold')
        require(not any(t['name'] == 'Probe ownership target' for t in subset['target-released']['snapshot']['textures']),
                name + ': target still visible after release')
        held = subset['held']
        if report['mode'] == 'Ocean':
            buffers = held['reachableBuffers']
            require(len(buffers) == 6 and all(b['valid'] for b in buffers), name + ': expected six live buffers')
            require(sum(b['logicalBytes'] for b in buffers) == 100288, name + ': unexpected logical buffer payload')
            for label in ['ocean-unloaded', 'unloaded-held']:
                unloaded = subset[label]
                require(unloaded['snapshot']['oceans'] == 0 and not unloaded['reachableBuffers']
                        and not any(b['valid'] for b in unloaded['trackedBufferState']), name + ': buffer release failure')
        else:
            require(report['featureDisabled'] and all(s['snapshot']['oceans'] == 0 and not s['reachableBuffers'] for s in subset.values()),
                    name + ': invalid empty control')
        cycles.append(dict(cycle=cycle, buffers=held['reachableBuffers'],
                           trackedAfterUnload=subset.get('unloaded-held', {}).get('trackedBufferState'),
                           stages=[dict(stage=s['stage'], rtCount=len(s['snapshot']['textures']),
                                        rtEngineBytes=sum(t['nativeBytes'] for t in s['snapshot']['textures']),
                                        tempBuffers=temps(s), driverMemory=s['driverMemory'],
                                        walkLimits=s['walkLimits']) for s in subset.values()]))
    cases.append(dict(name=name, mode=report['mode'], readback=report['readback'], hdr=report['hdr'],
                      api=report['api'], errors=report['errors'], warnings=warnings,
                      reportSha256=hashlib.sha256(path.read_bytes()).hexdigest(), cycles=cycles))

summary = dict(recordedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
               validation='FAILED' if failures else 'PASSED', failures=failures, cases=cases,
               limits=[
                   'Ten short offscreen controls, not performance windows or onscreen rendering acceptance.',
                   'TempBuffer observed after Submit before any readback, absent with HDR disabled and after two-second hold. Native allocator unproven.',
                   'Buffer scope: field-reachable Crest objects plus known activated FFT cache, depth12/nodes30000/collection1024. Vector3 query array truncation disclosed per stage.',
                   'Six Ocean_Calm ComputeBuffers, count*stride logical payload only. Does not inventory driver/mesh buffers or all Crest configurations.',
                   'DXGI CurrentUsage is current-process Local/NonLocal segment budget accounting; not independent physical VRAM residency. Queries are not GPU fences and can lag.',
                   'Disposal invalidates buffer wrappers; process driver usage can remain due to delayed work/pools. Three cycles do not establish a leak or general leak-free acceptance.',
                   'Historical shutdown evictable warning remains NOT REPRODUCED/not fixed. Normal builds and previous full suite unchanged.'])
(EVIDENCE / f'{PREFIX}-summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
print(summary['validation'], 'cases=', len(cases), 'failures=', failures)
sys.exit(bool(failures))
