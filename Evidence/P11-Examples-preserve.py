from pathlib import Path
import json,zipfile,subprocess,hashlib
root=Path('.').resolve()
keep=lambda p: p in {'Evidence/EditMode-DX11.xml','Evidence/EditMode-DX12.xml','Evidence/EditMode-DX11-interactive.xml','Evidence/EditMode-DX12-interactive.xml','Evidence/Build-checkpoint.json'} or any(p.startswith('Evidence/Player-'+label+'-'+api+'/') for label in ('Development','Release') for api in ('DX11','DX12'))
state=json.loads(Path('Evidence/P11-Examples-operation.json').read_text(encoding='utf-8-sig'))
assert state.get('step')=='Milestone complete' and state.get('state')=='complete',state
before=zipfile.ZipFile('Evidence/P11-Examples-evidence-before.zip');foreign=zipfile.ZipFile('Evidence/P11-Examples-foreign-untracked-before.zip')
initial=set(json.loads(Path('Evidence/P11-Examples-evidence-initial-paths.json').read_text()))
changed={}
for archive in (before,foreign):
 for name in archive.namelist():
  if any(part.startswith('P11-Examples-') for part in Path(name).parts):continue
  p=Path(name)
  if p.is_file() and p.read_bytes()!=archive.read(name):changed[name]=p
new=[p for p in Path('Evidence').rglob('*') if p.is_file() and p.as_posix() not in initial and not any(part.startswith('P11-Examples-') for part in p.parts) and not keep(p.as_posix())]
files=set(changed.values())|set(new)
files.update(p for p in Path('Evidence').rglob('*.log') if p.name.startswith('P11-Examples-') or p.parent.name.startswith('P11-Examples-'))
files.update(Path('Evidence')/name for name in ['EditMode-DX11.log','EditMode-DX12.log','EditMode-DX11-interactive.log','EditMode-DX12-interactive.log','Development-DX11.log','Release-DX11.log','ValidateAssets-DX11.log'])
files.update(Path('Evidence/Player-'+label+'-'+api+'/Player.log') for label in ('Development','Release') for api in ('DX11','DX12'))
files.update(Path('Library/ScriptAssemblies').glob('Crest*.dll'))
for label in ('Development','Release'):
 files.update(Path('Builds/'+label+'/CrestPort_Data/Managed').glob('Crest*.dll'))
files.update(Path('Builds/P11-OriginalExamples/OriginalExamples_Data/Managed').glob('Crest*.dll'))
for base in ('Assets/Crest/Crest-Examples/Shared/Scripts/CamController.cs','Assets/Crest/Crest-Examples/Examples/Scripts/ExamplesController.cs','Assets/Crest/Crest/Scripts/LodData/RegisterClipSurfaceInput.cs','Assets/Crest/URP/Tests/Editor/ExampleInputDevicesTests.cs','Assets/Crest/URP/Tests/Editor/SurfaceInputTests.cs','Assets/Crest/URP/Runtime/ResourceShutdownProbe.OriginalExamples.cs','Assets/Crest/URP/Runtime/ResourceShutdownProbe.cs','Assets/Crest/URP/Editor/ResourceShutdownProbeBuild.cs'):
 files.update([Path(base),Path(base+'.meta')])
files.add(Path('Assets/CrestPortDemos/ReflectionVertexVariants.asset'))
manifest=[]
with zipfile.ZipFile('Evidence/P11-Examples-regression-evidence.zip','x',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(files):
  if p.is_file():
   z.write(p,p.as_posix());manifest.append({'path':p.as_posix(),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
 z.writestr('manifest.json',json.dumps(manifest,indent=2))
for archive in (before,foreign):
 for name in archive.namelist():
  if any(part.startswith('P11-Examples-') for part in Path(name).parts):continue
  if name in changed and not keep(name):Path(name).write_bytes(archive.read(name))
for p in new:
 assert p.resolve().is_relative_to(root/'Evidence');p.unlink()
with zipfile.ZipFile('Evidence/P11-Examples-regression-before.zip') as z:
 for name in ('Evidence/EditMode-DX11-focused.xml','Evidence/EditMode-DX12-focused.xml'):Path(name).write_bytes(z.read(name))
p=Path('Assets/CrestPortDemos/ReflectionVertexVariants.asset');head=subprocess.check_output(['git','show','HEAD:'+p.as_posix()]);normalize=lambda b:b.decode('utf-8-sig').replace('\r\n','\n').replace('m_Values: \n','m_Values:\n')
assert normalize(p.read_bytes())==normalize(head),'Unexpected variant content delta; preserve and inspect'
p.write_bytes(head)
# Reduce the temporary broad snapshot: unchanged historical files remain in Git; retain exact non-HEAD content only.
retained={}
for name in subprocess.check_output(['git','diff','--name-only','--','Evidence'],text=True).splitlines():
 if name not in before.namelist() or name in ('Evidence/EditMode-DX11-focused.xml','Evidence/EditMode-DX12-focused.xml'):continue
 data=before.read(name)
 try:head=subprocess.check_output(['git','show','HEAD:'+name],stderr=subprocess.DEVNULL)
 except subprocess.CalledProcessError:head=b''
 norm=lambda b:b.replace(b'\r\n',b'\n')
 if norm(data)!=norm(head):retained[name]=data
before.close()
with zipfile.ZipFile('Evidence/P11-Examples-evidence-before.zip','w',zipfile.ZIP_DEFLATED) as z:
 for name,data in retained.items():z.writestr(name,data)
 z.writestr('provenance.json',json.dumps({'baseCommit':'98f3084','scope':'Exact non-HEAD initial historical evidence. Initial full inventory hashes in evidence-before-files.json; unchanged baseline data remains in base Git commit, no duplicate gallery retained. Canonical focused XML exact initial bytes are in regression-before.zip.','retained':list(retained)},indent=2))
Path('Evidence/P11-Examples-regression-files.json').write_text(json.dumps({'archive':'Evidence/P11-Examples-regression-evidence.zip','changedHistoricalReportsRestored':sorted(name for name in changed if not keep(name)),'newHistoricalFilesArchivedAndRemoved':[p.as_posix() for p in new],'files':manifest},indent=2))
print('Archived',len(manifest),'raw/source/DLL files; restored',len(changed),'historical deltas; removed',len(new),'own generated historical filenames; compact baseline entries',len(retained))
