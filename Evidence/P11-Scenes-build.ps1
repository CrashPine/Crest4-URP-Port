$ErrorActionPreference='Stop'
$p=Start-Process 'C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe' -WindowStyle Hidden -ArgumentList '-batchmode','-quit','-projectPath',('"'+(Get-Location).Path+'"'),'-force-d3d11','-crestTraceMixedVariants','-executeMethod','Crest.URP.Editor.ResourceShutdownProbeBuild.BuildOriginalScenes','-logFile',('"'+(Join-Path (Get-Location) 'Evidence/P11-Scenes-build.log')+'"') -Wait -PassThru
if($p.ExitCode -ne 0){throw "Original scenes build exit$($p.ExitCode)"}
