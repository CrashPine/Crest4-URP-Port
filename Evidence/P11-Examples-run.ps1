param([ValidateSet('DX11','DX12')][string]$API)
$ErrorActionPreference='Stop'
$root=(Get-Location).Path;$folder=Join-Path $root "Evidence/P11-Examples-$API";New-Item -ItemType Directory -Force -Path $folder | Out-Null
$started=[DateTime]::UtcNow
$arguments=@('-batchmode',('-force-'+$(if($API -eq 'DX11'){'d3d11'}else{'d3d12'})),'-screen-fullscreen','0','-screen-width','960','-screen-height','540','-crestResourceProbe','-crestProbeMode','Ocean','-crestOriginalExamples','-crestProbeOutput',('"'+(Join-Path $folder 'report.json')+'"'),'-crestProbeImages',('"'+$folder+'"'),'-logFile',('"'+(Join-Path $folder 'Player.log')+'"'))
$p=Start-Process (Join-Path $root 'Builds/P11-OriginalExamples/OriginalExamples.exe') -WindowStyle Hidden -ArgumentList $arguments -PassThru
try {
 if(!$p.WaitForExit(180000)){Stop-Process -Id $p.Id -Force;throw 'Original Examples Player timeout180s'}
 [ordered]@{api=$API;pid=$p.Id;startUtc=$started.ToString('o');endUtc=[DateTime]::UtcNow.ToString('o');exitCode=$p.ExitCode} | ConvertTo-Json | Set-Content "Evidence/P11-Examples-$API-operation.json"
 if($p.ExitCode -ne 0){throw "Original Examples exit$($p.ExitCode); preserve report/log"}
 if(!(Test-Path (Join-Path $folder 'report.json')) -or (Get-Item (Join-Path $folder 'report.json')).LastWriteTimeUtc -lt $started){throw 'No fresh original scene report'}
} finally {if(!$p.HasExited){Stop-Process -Id $p.Id -Force}}
