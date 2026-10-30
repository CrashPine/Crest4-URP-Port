"""Validate six scoped underwater-cost windows from raw diagnostic reports. No Unity rerun."""
import datetime
import hashlib
import json
from pathlib import Path
import re
import sys

ROOT=Path(__file__).resolve().parent.parent
PREFIX=sys.argv[1] if len(sys.argv)>1 else 'P10-UnderwaterCost-final'
if not re.fullmatch(r'[A-Za-z0-9_-]+',PREFIX):
    raise ValueError('Invalid evidence prefix')
EVIDENCE=ROOT/'Evidence'
runs=json.loads((EVIDENCE/(PREFIX+'-runs.json')).read_text(encoding='utf-8-sig'))
failures=[]
cases=[]


def require(value,description):
    if not value:
        failures.append(description)


require(len(runs)==6,'Expected six windows')
for run in runs:
    name=run['name']
    require(run['exitCode']==0 and not run['timedOut'],name+': process failed')
    path=EVIDENCE/(name+'.json')
    if not path.exists():
        require(False,name+': missing report')
        continue
    report=json.loads(path.read_text())
    log=(EVIDENCE/(name+'.log')).read_text(errors='replace')
    require('RESOURCE_PROFILE_COMPLETED' in log,name+': missing completion marker')
    issues=re.findall(r'^.*(?:variant .*not found|Exception:|Assertion failed|Leak Detected|unreleased.*evictable).*$',log,re.M|re.I)
    warnings=[]
    for match in re.finditer(r'^CommandBuffer: temporary render texture .*not found.*$',log,re.M):
        before_sample=match.start()<log.find('PROFILE_STAGE sample-start')
        warnings.append(dict(text=match.group(0),beforeSample=before_sample))
        require(before_sample,name+': temporary-texture warning during/after measured window')
    require(not issues,name+': shader/error/shutdown diagnostics present')
    require(report['errors']==0,name+': diagnostic assertion failed')
    require(report['unity']=='6000.6.5f1' and report['mode']=='Ocean' and not report['batch'],name+': invalid configuration')
    require(report['api']==('Direct3D11' if name.endswith('DX11') else 'Direct3D12'),name+': API mismatch')
    require(report['sampleSeconds']>=10 and report['warmSeconds']>=5,name+': too short')
    require(report['sampleRequests']>=20 and report['mainCameraRenders']==report['sampleRequests'],name+': missing actual renders')
    workload=report['costWorkload']
    require(workload in ['UnderwaterOff','Underwater1','Underwater4'],name+': unknown workload')
    samples=4 if workload=='Underwater4' else 1
    expected=0 if workload=='UnderwaterOff' else report['sampleRequests']
    preserve=expected if samples==4 else 0
    before,after=report['beforeCost'],report['afterCost']
    for field,count in [('compositeRecorded',expected),('compositeExecuted',expected),('preserveRecorded',preserve),('preserveExecuted',preserve)]:
        require(after[field]-before[field]==count,name+': '+field+' mismatch')
    require(before['targetSamples']==after['targetSamples']==report['msaa']==samples,name+': wrong MSAA')
    if expected:
        require(before['underwaterActive'] and after['underwaterActive'] and after['underwaterEnabled'],name+': inactive effect')
        require(after['colorSamples']==samples and after['colorWidth']==960 and after['colorHeight']==540,name+': wrong descriptor')
    else:
        require(not before['underwaterEnabled'] and not after['underwaterEnabled'],name+': invalid off control')
    metrics={m['name']:m for m in report['metrics']}
    for metric in ['SubmitRenderRequest CPU','GC bytes during SubmitRenderRequest']:
        require(metrics[metric]['count']==report['sampleRequests'],name+': incomplete metric '+metric)
    require(report['gcCounterResets']==0,name+': GC counter reset')
    inventories=[]
    for label in ['before','after']:
        textures=report[label]['textures']
        inventories.append(dict(stage=label,count=len(textures),engineBytes=sum(t['nativeBytes'] for t in textures),
                                underwaterTextures=[t for t in textures if 'Underwater' in t['name']]))
    cases.append(dict(name=name,api=report['api'],workload=workload,requests=report['sampleRequests'],
                      warmSeconds=report['warmSeconds'],sampleSeconds=report['sampleSeconds'],
                      submitCpu=metrics['SubmitRenderRequest CPU'],submitGc=metrics['GC bytes during SubmitRenderRequest'],
                      beforeCost=before,afterCost=after,inventories=inventories,
                      rawSha256=hashlib.sha256(path.read_bytes()).hexdigest(),issues=issues,warnings=warnings))

require({(c['api'],c['workload']) for c in cases}=={(a,w) for a in ['Direct3D11','Direct3D12'] for w in ['UnderwaterOff','Underwater1','Underwater4']},'Missing API/workload combination')
summary=dict(recordedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),validation='FAILED' if failures else ('MEASURED_WINDOWS_PASSED_WITH_STARTUP_WARNING' if any(c['warnings'] for c in cases) else 'PASSED'),failures=failures,cases=cases,
             limits=['Fixed order, one short window per configuration; descriptive measurements, no statistical repeatability/API ranking.',
                     'Same submerged camera and runtime cloned Calm+UNDERWATER surface in all three states; effect toggle changes whole mask/composite path.',
                     'MSAA1 vs4 changes the pipeline and triggers copy-back; CPU difference is not isolated copy GPU cost.',
                     'Counters observe RenderGraph registration and CPU command-recording callbacks, not physical GPU pass completion or GPU duration.',
                     'Submit CPU and native current-GC deltas exclude other Update/simulation/worker activity and readback wait; no whole-frame/FPS claim.',
                     'Conditional counters add instrumentation overhead; normal underwater source is text-identical after removing CREST_RESOURCE_PROBE branches.',
                     'RT bytes are Unity engine-reported, not driver VRAM. Camera-stack preservation, reflection costs, supported variants and GPU timing remain separate coverage.'])
summary['limits'].append('Any startup temporary-texture warning is retained separately and is NOT a clean lifecycle/rendering acceptance; warning owner remains unproven.')
(EVIDENCE/(PREFIX+'-summary.json')).write_text(json.dumps(summary,indent=2),encoding='utf-8')
print(summary['validation'],'cases=',len(cases),'failures=',failures)
sys.exit(bool(failures))
