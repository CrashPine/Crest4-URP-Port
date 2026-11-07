$ErrorActionPreference='Stop'
[ordered]@{state='building';utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content Evidence/P11-Stack-operation.json
$p=Start-Process 'C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe' -WindowStyle Hidden -ArgumentList '-batchmode','-quit','-projectPath',('"'+(Get-Location).Path+'"'),'-force-d3d11','-executeMethod','Crest.URP.Editor.ResourceShutdownProbeBuild.BuildCameraStack','-logFile',('"'+(Join-Path (Get-Location) 'Evidence/P11-Stack-build.log')+'"') -Wait -PassThru
if($p.ExitCode -ne 0){throw "Modes build exit$($p.ExitCode)"}
[ordered]@{state='build-complete';utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content Evidence/P11-Stack-operation.json
