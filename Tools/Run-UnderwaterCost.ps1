param(
    [string]$Prefix='P10-UnderwaterCost',
    [ValidateSet('All','DX11','DX12')][string]$GraphicsAPI='All',
    [ValidateSet('All','UnderwaterOff','Underwater1','Underwater4')][string]$Workload='All',
    [ValidateRange(1,600)][int]$Seconds=10
)
$ErrorActionPreference='Stop'
$repository=Split-Path $PSScriptRoot -Parent
if (Get-Process Unity,ResourceProbe -ErrorAction SilentlyContinue) { throw 'Close existing Unity/ResourceProbe processes first.' }
if ($Prefix -notmatch '^[A-Za-z0-9_-]+$') { throw 'Invalid evidence prefix.' }
if ((Test-Path -LiteralPath (Join-Path $repository "Evidence/$Prefix-runs.json")) -or
    (Test-Path -LiteralPath (Join-Path $repository "Evidence/$Prefix-summary.json"))) { throw 'Use a new prefix; an evidence index/summary already exists.' }
$apis=if ($GraphicsAPI -eq 'All') { @('DX11','DX12') } else { @($GraphicsAPI) }
$workloads=if ($Workload -eq 'All') { @('UnderwaterOff','Underwater1','Underwater4') } else { @($Workload) }
$runs=@()
foreach ($api in $apis) {
    foreach ($case in $workloads) {
        $name="$Prefix-$case-$api"
        $report=Join-Path $repository "Evidence/$name.json"
        $log=Join-Path $repository "Evidence/$name.log"
        if ((Test-Path -LiteralPath $report) -or (Test-Path -LiteralPath $log)) { throw "Evidence already exists: $name" }
        $arguments=@('-crestResourceProbe','-crestProfile','-crestCost',$case,'-crestProfileSeconds',"$Seconds",
            '-crestProbeMode','Ocean','-screen-fullscreen','0','-screen-width','960','-screen-height','540',
            "-force-d3d$($api.Substring(2))",'-crestProbeOutput',('"'+$report+'"'),'-logFile',('"'+$log+'"'))
        $start=[DateTime]::UtcNow.ToString('o')
        $process=Start-Process (Join-Path $repository 'Builds/P10-ResourceProbe/ResourceProbe.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru
        $timedOut=-not $process.WaitForExit(($Seconds+60)*1000)
        if ($timedOut) { $process.Kill(); $process.WaitForExit() }
        $process.Refresh()
        $runs += [ordered]@{name=$name;pid=$process.Id;startUtc=$start;endUtc=[DateTime]::UtcNow.ToString('o');
            exitCode=$process.ExitCode;timedOut=$timedOut;arguments=$arguments;reportExists=(Test-Path -LiteralPath $report)}
        $runs | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $repository "Evidence/$Prefix-runs.json") -Encoding utf8
        Write-Output "$name exit=$($process.ExitCode) timeout=$timedOut"
        if ($timedOut -or $process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $report)) { throw "Failed $name; remaining cases were not started." }
    }
}
if ($runs | Where-Object { $_.timedOut -or $_.exitCode -ne 0 -or -not $_.reportExists }) { throw 'At least one case failed; inspect retained evidence.' }
