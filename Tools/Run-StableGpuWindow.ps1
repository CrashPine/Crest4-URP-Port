param([ValidateSet('DX11','DX12')][string]$API)
$ErrorActionPreference='Stop'
$root=(Get-Location).Path
$prefix=Join-Path $root "Evidence/P10-StableGpu-$API"
$telemetry=$null;$player=$null
try {
 $telemetry=Start-Process nvidia-smi -WindowStyle Hidden -ArgumentList '--query-gpu=timestamp,name,driver_version,temperature.gpu,clocks.current.graphics,clocks.current.memory,utilization.gpu,power.draw','--format=csv','-lms','500' -RedirectStandardOutput "$prefix-telemetry.csv" -RedirectStandardError "$prefix-telemetry.stderr.log" -PassThru
 $args=@('-force-'+$(if($API -eq 'DX11'){'d3d11'}else{'d3d12'}),'-screen-fullscreen','0','-screen-width','960','-screen-height','540','-crestResourceProbe','-crestProbeMode','Ocean','-crestStableGpu','-crestProbeOutput',('"'+$prefix+'.json"'),'-logFile',('"'+$prefix+'.log"'))
 $start=[DateTime]::UtcNow
 $player=Start-Process -FilePath (Join-Path $root 'Builds/P10-ResourceProbe/ResourceProbe.exe') -WindowStyle Normal -ArgumentList $args -PassThru
 if(!$player.WaitForExit(150000)){Stop-Process -Id $player.Id -Force;throw 'Stable GPU Player exceeded150s bound'}
 if($player.ExitCode -ne 0){throw "Stable GPU Player exit$($player.ExitCode)"}
 if(!(Test-Path "$prefix.json") -or (Get-Item "$prefix.json").LastWriteTimeUtc -lt $start){throw 'No fresh result'}
 [ordered]@{api=$API;status='COMPLETED_NOT_YET_ACCEPTED';startUtc=$start.ToString('o');endUtc=[DateTime]::UtcNow.ToString('o');playerPid=$player.Id;telemetryPid=$telemetry.Id} | ConvertTo-Json | Set-Content "$prefix-operation.json"
} finally {
 if($player -and !$player.HasExited){Stop-Process -Id $player.Id -Force}
 if($telemetry -and !$telemetry.HasExited){Stop-Process -Id $telemetry.Id}
}
