from pathlib import Path
import json,hashlib,datetime,xml.etree.ElementTree as ET,zipfile,re
root=Path('.').resolve()
def record(path):
 p=Path(path);return {'path':p.as_posix(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest().upper(),'bytes':p.stat().st_size,'modifiedUtc':datetime.datetime.fromtimestamp(p.stat().st_mtime,datetime.timezone.utc).isoformat()}
def refresh(row,path):
 result=dict(row);r=record(path)
 for k in ('sha256','bytes','modifiedUtc'):result[k]=r[k]
 return result
operation=json.loads(Path('Evidence/P11-Examples-operation.json').read_text(encoding='utf-8-sig'))
assert operation.get('step')=='Milestone complete' and operation.get('state')=='complete',operation
reports=[]
for api in ('DX11','DX12'):
 for interactive,count in ((False,121),(True,2)):
  p=Path('Evidence/EditMode-'+api+('-interactive' if interactive else '')+'.xml');run=ET.parse(p).getroot()
  assert run.get('result')=='Passed' and int(run.get('total'))==count and int(run.get('failed'))==0 and int(run.get('skipped'))==0,(p,run.attrib)
  assert all(c.get('result')=='Passed' for c in run.findall('.//test-case'))
  reports.append({**record(p),'total':count,'result':run.get('result'),'api':api,'interactive':interactive})
with zipfile.ZipFile('Evidence/P11-Examples-before.zip') as z:j=json.loads(z.read('Evidence/Build-checkpoint.json'))
j['recordedUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
for p in ('Assets/Crest/Crest-Examples/Shared/Scripts/CamController.cs','Assets/Crest/Crest-Examples/Examples/Scripts/ExamplesController.cs','Assets/Crest/Crest/Scripts/LodData/RegisterClipSurfaceInput.cs'):
 if not any(r['path']==p for r in j['runtimeSources']):j['runtimeSources'].append({'path':p})
j['runtimeSources']=[refresh(r,r['path']) for r in j['runtimeSources']]
j['builds']=[refresh(r,'Builds/'+r['configuration']+'/'+r['assembly']) for r in j['builds']]
j['packedAssets']=[refresh(r,'Builds/'+r['configuration']+'/'+r['path']) for r in j['packedAssets']]
j['materialSources']=[refresh(r,r['path']) for r in j['materialSources']]
j['buildLogs']=[record(p) for p in ('Evidence/Development-DX11.log','Evidence/Release-DX11.log')]
for r in j['buildLogs']:
 assert 'Result: Success' in Path(r['path']).read_text(errors='replace') or "Build completed with a result of 'Succeeded'" in Path(r['path']).read_text(errors='replace'),r['path']
for smoke in j['smokes']:
 smoke['report']=record(smoke['report']['path']);s=Path(smoke['report']['path']).read_text()
 assert re.search(r'(?m)^Errors=0\.',s)
 assert len(re.findall(r'(?m)^CaptureFrame:',s))==7
 smoke['images']=[record(r['path']) for r in smoke['images']]
 smoke['playerLog']=record(smoke['playerLog']['path']);text=Path(smoke['playerLog']['path']).read_text(errors='replace')
 assert 'RenderData validation failed' not in text
 smoke['warnings']=[line for line in text.splitlines() if 'evictable' in line.lower() or 'Persistent allocates' in line]
 smoke['reflectionReport']=record(Path(smoke['report']['path']).parent/'reflection-options.txt')
 buildlog=Path('Evidence/'+smoke['configuration']+'-DX11.log');assert Path(smoke['report']['path']).stat().st_mtime>buildlog.stat().st_mtime
j['previousNormalCheckpoint']={'path':'Evidence/P11-Examples-before.zip','entry':'Evidence/Build-checkpoint.json'}
j['validationStatus']='Full121+actual interactive2/API Passed; Development/Release Success;4 sequential Player smokes Errors0,7 scenes+4 reflection captures each. Isolated original Examples24/24/API with3 fill controls/errors0.'
j['testReports']=reports
j['limitation']='P11.3-A baseline scope only. Fixed absent input devices in example controllers and first/recreated Sphere clip keyword initialization. B-D/F-G and finalP11.4/P12 remain pending; E optional. Not physical keyboard/GUI/all configurations/physical XR/Intel or GPU-cost acceptance.'
Path('Evidence/Build-checkpoint.json').write_text(json.dumps(j,indent=2),encoding='utf-8')
p=Path('Evidence/P11-Examples-summary.json');summary=json.loads(p.read_text());summary['runtimeMilestone']=j['validationStatus'];summary['testReports']=reports;summary['normalCheckpoint']=record('Evidence/Build-checkpoint.json');summary['runtimeWarnings']={}
for api in ('DX11','DX12'):
 text=Path('Evidence/EditMode-'+api+'.log').read_text(errors='replace');summary['runtimeWarnings'][api]={term:text.count(term) for term in ['Persistent allocates','RenderData validation failed','variant <no keywords> not found','error CS']}
 assert not any(summary['runtimeWarnings'][api].values()),summary['runtimeWarnings'][api]
summary['recordedUtc']=j['recordedUtc'];p.write_text(json.dumps(summary,indent=2))
print(j['validationStatus'])
