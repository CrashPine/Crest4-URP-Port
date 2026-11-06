$ErrorActionPreference='Stop'
$editor='C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe'
function Step([string]$name,[scriptblock]$action) {
 [ordered]@{step=$name;state='running';utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content Evidence/P11-Examples-operation.json
 & $action
 [ordered]@{step=$name;state='complete';utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content Evidence/P11-Examples-operation.json
}
try {
 Step 'Isolated original Examples build' { & ./Evidence/P11-Examples-build.ps1 }
 Step 'Original Examples Player DX11' { & ./Evidence/P11-Examples-run.ps1 -API DX11 }
 Step 'Original Examples Player DX12' { & ./Evidence/P11-Examples-run.ps1 -API DX12 }
 Step 'Full batch DX11' { & ./Tools/Run-Unity.ps1 -Editor $editor -Action EditMode -GraphicsAPI DX11 }
 Step 'Full batch DX12' { & ./Tools/Run-Unity.ps1 -Editor $editor -Action EditMode -GraphicsAPI DX12 }
 Step 'Interactive DX11' { & ./Tools/Run-Unity.ps1 -Editor $editor -Action EditMode -GraphicsAPI DX11 -Interactive }
 Step 'Interactive DX12' { & ./Tools/Run-Unity.ps1 -Editor $editor -Action EditMode -GraphicsAPI DX12 -Interactive }
 Step 'ValidateAssets' { & ./Tools/Run-Unity.ps1 -Editor $editor -Action ValidateAssets -GraphicsAPI DX11 }
 Step 'Restore test-mutated materials' {
  & py -3.13 -c 'from pathlib import Path; import zipfile; ps=["Assets/Crest/Crest-Examples/Examples/Materials/Examples_Material_TransparentPrimitive.mat","Assets/Crest/Crest-Examples/Shared/Materials/OffWhite.mat"]; z=zipfile.ZipFile("Evidence/P11-Examples-regression-before.zip"); a=zipfile.ZipFile("Evidence/P11-Examples-test-mutated-materials.zip","x",zipfile.ZIP_DEFLATED); [(a.write(p,p),Path(p).write_bytes(z.read(p))) for p in ps]; a.close()'
  if($LASTEXITCODE -ne 0){throw 'Material restoration failed'}
 }
 Step 'Build Development' { & ./Tools/Run-Unity.ps1 -Editor $editor -Action Development -GraphicsAPI DX11 }
 Step 'Build Release' { & ./Tools/Run-Unity.ps1 -Editor $editor -Action Release -GraphicsAPI DX11 }
 foreach($label in @('Development','Release')) { foreach($api in @('DX11','DX12')) {
  Step "Smoke $label $api" { & ./Tools/Run-Player.ps1 -Player "Builds/$label/CrestPort.exe" -GraphicsAPI $api -Label $label -ReflectionOptions }
 }}
 Step 'Milestone complete' { 'All requested commands completed; inspect raw XML, images and evidence before acceptance.' }
} catch {
 [ordered]@{state='failed';utc=[DateTime]::UtcNow.ToString('o');error=$_.ToString()} | ConvertTo-Json | Set-Content Evidence/P11-Examples-operation.json
 throw
}
