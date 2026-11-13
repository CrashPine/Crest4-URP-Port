$ErrorActionPreference='Stop'
$running=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -notmatch '-adb2' -and $_.CommandLine -like '*Crest4-URP-Port*'})
if($running.Count){throw 'Target Editor already open'}
& ./Evidence/P11-DepthLifecycle-build.ps1
$exe=(Resolve-Path Builds/P11-DepthCacheLifecycle/DepthCacheLifecycle.exe).Path
foreach($api in @('DX11','DX12')){
    $dir=(New-Item -ItemType Directory -Path "Evidence/P11-DepthLifecycle-$api").FullName
    $log=Join-Path $dir 'Player.log'
    $report=Join-Path $dir 'report.json'
    $flag=if($api -eq 'DX11'){'-force-d3d11'}else{'-force-d3d12'}
    $args=@('-batchmode',$flag,'-screen-width','640','-screen-height','360','-crestResourceProbe','-crestProbeMode','OceanUnload','-crestProbeSubmit','-crestProbeOutput',('"{0}"' -f $report),'-logFile',('"{0}"' -f $log))
    $p=Start-Process $exe -WindowStyle Hidden -PassThru -ArgumentList $args
    if(!$p.WaitForExit(90000)){throw "Player still running for $api. Inspect PID $($p.Id)."}
    $state=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    $warm=@($state.warm.textures | Where-Object {$_.created -and $_.name -like '*oceanDepth*'})
    $drained=@($state.drained.textures | Where-Object {$_.created -and $_.name -like '*oceanDepth*'})
    $warnings=@(Select-String -LiteralPath $log -SimpleMatch -Pattern 'Releasing render texture that is set to be RenderTexture.active!').Count
    $expected=if($api -eq 'DX11'){'Direct3D11'}else{'Direct3D12'}
    $pass=$p.ExitCode -eq 0 -and $state.errors -eq 0 -and $state.api -eq $expected -and $state.submit -and $state.warm.oceans -eq 1 -and $state.drained.oceans -eq 0 -and $warm.Count -eq 2 -and $drained.Count -eq 0 -and $warnings -eq 0
    [ordered]@{api=$state.api; exit=$p.ExitCode; errors=$state.errors; warmCache=$warm; drainedCache=$drained; activeReleaseWarnings=$warnings; passed=$pass} | ConvertTo-Json -Depth 7 | Tee-Object -FilePath (Join-Path $dir 'result.json')
    if(!$pass){throw "Depth lifecycle control failed for $api."}
}
