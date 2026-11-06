$ErrorActionPreference='Stop'
$editor='C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe'
$p=Start-Process $editor -WindowStyle Hidden -ArgumentList '-batchmode','-quit','-projectPath',('"'+(Get-Location).Path+'"'),'-force-d3d11','-executeMethod','Crest.URP.Editor.ResourceShutdownProbeBuild.BuildOriginalExamples','-logFile',('"'+(Join-Path (Get-Location) 'Evidence/P11-Examples-build.log')+'"') -Wait -PassThru
if($p.ExitCode -ne 0){throw "Original scene diagnostic build exit$($p.ExitCode)"}
