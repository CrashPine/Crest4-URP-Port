param([ValidateSet('DX11','DX12')][string]$API='DX11',[ValidateSet('input','main','boat','threeboats','wakes-off','wakes-on','wakes-off-repeat','rivers','whirl-combined','whirl-isolated')][string]$Case='input')
$ErrorActionPreference='Stop'
$folder=Join-Path (Get-Location) "Evidence/P11-Scenes-$API-$Case";New-Item -ItemType Directory -Force $folder | Out-Null
$start=[DateTime]::UtcNow
[ordered]@{api=$API;case=$Case;state='running';utc=$start.ToString('o')} | ConvertTo-Json | Set-Content Evidence/P11-Scenes-operation.json
$args=@('-batchmode',$(if($API -eq 'DX11'){'-force-d3d11'}else{'-force-d3d12'}),'-screen-width','960','-screen-height','540','-screen-fullscreen','0','-crestResourceProbe','-crestOriginalSceneCase',$Case,'-crestProbeOutput',('"'+(Join-Path $folder 'report.json')+'"'),'-crestProbeImages',('"'+$folder+'"'),'-logFile',('"'+(Join-Path $folder 'Player.log')+'"'))
$p=Start-Process (Join-Path (Get-Location) 'Builds/P11-OriginalScenes/OriginalScenes.exe') -ArgumentList $args -WindowStyle Hidden -PassThru
try {
 if(!$p.WaitForExit(240000)){Stop-Process -Id $p.Id -Force;throw 'Original scene timeout240s'}
 [ordered]@{api=$API;case=$Case;state=$(if($p.ExitCode -eq 0){'complete'}else{'failed'});startUtc=$start.ToString('o');endUtc=[DateTime]::UtcNow.ToString('o');exitCode=$p.ExitCode} | ConvertTo-Json | Set-Content Evidence/P11-Scenes-operation.json
 if($p.ExitCode -ne 0){throw "Original $Case $API exit$($p.ExitCode); preserve failure"}
 $report=Get-Item (Join-Path $folder 'report.json');if($report.LastWriteTimeUtc -lt $start){throw 'Stale report'}
 $json=Get-Content -Raw $report.FullName | ConvertFrom-Json;if($json.errors -ne 0 -or $json.failure){throw 'Original scene report failure'}
}finally{if(!$p.HasExited){Stop-Process -Id $p.Id -Force}}
