param(
    [string]$Prefix='P10-ColdBindings',
    [ValidateSet('DX11','DX12')][string]$GraphicsAPI='DX12',
    [ValidateSet('UnderwaterOff','Underwater1','Underwater4')][string]$Workload='Underwater1',
    [ValidateSet('0')][int[]]$Control=@(0)
)
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
if (Get-Process Unity,ResourceProbe -ErrorAction SilentlyContinue) { throw 'Close existing Unity/ResourceProbe first.' }
if ($Prefix -notmatch '^[A-Za-z0-9_-]+$' -or ($Control | Where-Object { $_ -lt 0 -or $_ -gt 4 })) { throw 'Invalid prefix/control.' }
$index=Join-Path $repository "Evidence/$Prefix-runs.json"
if (Test-Path -LiteralPath $index) { throw 'Use a fresh prefix.' }
$runs=@()
foreach ($binding in $Control) {
    $name="$Prefix-$Workload-Control$binding-$GraphicsAPI"
    $report=Join-Path $repository "Evidence/$name.json"
    $log=Join-Path $repository "Evidence/$name.log"
    if ((Test-Path -LiteralPath $report) -or (Test-Path -LiteralPath $log)) { throw 'Existing evidence.' }
    $arguments=@('-crestResourceProbe','-crestColdBindings','-crestCost',$Workload,'-crestBindingControl',"$binding",'-crestProbeMode','Ocean',
        '-screen-fullscreen','0','-screen-width','960','-screen-height','540',"-force-d3d$($GraphicsAPI.Substring(2))",'-crestProbeOutput',('"'+$report+'"'),'-logFile',('"'+$log+'"'))
    $start=[DateTime]::UtcNow.ToString('o')
    $process=Start-Process (Join-Path $repository 'Builds/P10-ResourceProbe/ResourceProbe.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $timeout=-not $process.WaitForExit(45000)
    if ($timeout) { $process.Kill(); $process.WaitForExit() }
    $process.Refresh()
    $runs += [ordered]@{name=$name;pid=$process.Id;startUtc=$start;endUtc=[DateTime]::UtcNow.ToString('o');exitCode=$process.ExitCode;timedOut=$timeout;arguments=$arguments}
    $runs | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $index -Encoding utf8
    Write-Output "$name exit=$($process.ExitCode) timeout=$timeout"
    if ($timeout -or $process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $report)) { throw 'Cold diagnostic failed; raw evidence retained.' }
}
