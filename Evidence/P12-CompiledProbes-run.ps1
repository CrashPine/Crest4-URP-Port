$ErrorActionPreference='Stop'
$deadline=[DateTime]::UtcNow.AddMinutes(25)
while([DateTime]::UtcNow -lt $deadline){
 $s=Get-Content Evidence/P12-ProbeRegression-operation.json -Raw | ConvertFrom-Json
 if($s.state -eq 'failed'){throw "Normal regression stopped at $($s.step)"}
 if($s.state -eq 'complete' -and (Test-Path Evidence/P12-ProbeRegression-evidence.zip) -and (Get-Content Evidence/Build-checkpoint.json -Raw|ConvertFrom-Json).validationStatus -match 'full 125 '){break}
 Start-Sleep -Seconds 8
}
if([DateTime]::UtcNow -ge $deadline){throw 'Normal regression collection timeout'}
$busy=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -notmatch '-adb2' -and $_.CommandLine -like '*Crest4-URP-Port*'})
if($busy.Count){throw 'Target Editor is already open'}
$root=(Get-Location).Path
$editor='C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe'
$p=Start-Process $editor -WindowStyle Hidden -Wait -PassThru -ArgumentList '-batchmode','-quit','-projectPath',('"{0}"' -f $root),'-force-d3d11','-executeMethod','Crest.URP.Editor.ResourceShutdownProbeBuild.BuildLocalProbes','-logFile',('"{0}/Evidence/P12-CompiledProbes-build.log"' -f $root)
if($p.ExitCode -ne 0){throw "Probe build failed: $($p.ExitCode)"}
foreach($api in 'DX11','DX12'){
 $dir=(New-Item -ItemType Directory -Path "Evidence/P12-CompiledProbes-$api").FullName
 $flag=if($api -eq 'DX11'){'-force-d3d11'}else{'-force-d3d12'}
 $p=Start-Process Builds/P12-LocalProbes/LocalProbes.exe -WindowStyle Hidden -PassThru -ArgumentList '-batchmode',$flag,'-crestResourceProbe','-crestLocalProbes','-crestProbeOutput',('"{0}/report.json"' -f $dir),'-logFile',('"{0}/Player.log"' -f $dir)
 if(!$p.WaitForExit(90000)){throw "Probe Player still running PID $($p.Id)"}
 $q=Get-Content (Join-Path $dir 'report.json') -Raw|ConvertFrom-Json
 $q|ConvertTo-Json
 if($p.ExitCode -ne 0 -or !$q.passed -or $q.errors){throw "Compiled probes failed on $api"}
}
