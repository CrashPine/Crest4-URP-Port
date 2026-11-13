param([Parameter(Mandatory)][ValidateSet('DX11','DX12')][string]$Api)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$run=Join-Path $PSScriptRoot ('P11-NavigationInput-'+$Api+'-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))
$null=New-Item -ItemType Directory -Path $run
$exe=Join-Path $root 'Builds/P11-NavigationInput/NavigationInput.exe'
$flag=if($Api -eq 'DX11'){'-force-d3d11'}else{'-force-d3d12'}
$log=Join-Path $run 'Player.log'
$report=Join-Path $run 'report.json'
$finish=Join-Path $run 'finish'
$started=[DateTime]::UtcNow.ToString('o')
$player=Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -WindowStyle Normal -PassThru -ArgumentList @(
    $flag,'-screen-width','960','-screen-height','540','-screen-fullscreen','0','-crestResourceProbe','-crestNavigationInput',
    '-crestProbeOutput',('"{0}"' -f $report),'-crestInteractionFinish',('"{0}"' -f $finish),'-logFile',('"{0}"' -f $log)
)
$operation=[ordered]@{api=$Api;pid=$player.Id;startedUtc=$started;state='running';report=$report;finish=$finish}
$state=Join-Path $run 'operation.json'
$operation | ConvertTo-Json | Set-Content $state
if(!$player.WaitForExit(210000)){
    $operation.state='timeout'
    if(!$player.HasExited){$player.Kill();$player.WaitForExit()}
}else{
    $operation.state='exited'
    $operation.exitCode=$player.ExitCode
}
$operation.endedUtc=[DateTime]::UtcNow.ToString('o')
$operation | ConvertTo-Json | Set-Content $state
if($operation.state -ne 'exited' -or $operation.exitCode -ne 0){throw "Navigation $Api failed; see $state"}
