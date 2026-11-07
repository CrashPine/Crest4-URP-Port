param([ValidateSet('DX11','DX12')][string]$API='DX11')
$ErrorActionPreference='Stop'
$folder=Join-Path (Get-Location) "Evidence/P11-Lightmaps-$API";New-Item -ItemType Directory -Force $folder | Out-Null
$start=[DateTime]::UtcNow
$launchArguments=@('-batchmode',$(if($API -eq 'DX11'){'-force-d3d11'}else{'-force-d3d12'}),'-screen-width','960','-screen-height','540','-screen-fullscreen','0','-crestResourceProbe','-crestLightmaps','-crestProbeOutput',('"'+(Join-Path $folder 'report.json')+'"'),'-crestProbeImages',('"'+$folder+'"'),'-logFile',('"'+(Join-Path $folder 'Player.log')+'"'))
[ordered]@{state='running';api=$API;utc=$start.ToString('o')} | ConvertTo-Json | Set-Content Evidence/P11-Lightmaps-operation.json
$p=Start-Process (Join-Path (Get-Location) 'Builds/P11-Lightmaps/Lightmaps.exe') -ArgumentList $launchArguments -WindowStyle Hidden -PassThru
try {
 if(!$p.WaitForExit(180000)){Stop-Process -Id $p.Id -Force;throw 'Modes timeout180s'}
 [ordered]@{state=$(if($p.ExitCode -eq 0){'complete'}else{'failed'});api=$API;startUtc=$start.ToString('o');endUtc=[DateTime]::UtcNow.ToString('o');exitCode=$p.ExitCode} | ConvertTo-Json | Set-Content Evidence/P11-Lightmaps-operation.json
 if($p.ExitCode -ne 0){throw "Modes $API exit$($p.ExitCode); preserve failure"}
 $report=Get-Item (Join-Path $folder 'report.json');if($report.LastWriteTimeUtc -lt $start){throw 'Stale report'}
 $j=Get-Content -Raw $report.FullName | ConvertFrom-Json;if($j.errors -ne 0 -or $j.failure -or $j.rows.Count -ne 2){throw 'Incomplete modes acceptance'}
}finally{if(!$p.HasExited){Stop-Process -Id $p.Id -Force}}
