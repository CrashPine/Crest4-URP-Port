param(
    [string]$Editor = 'C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe',
    [ValidateSet('Both', 'Box', 'Atlas')][string]$Probe = 'Both',
    [switch]$AutomaticCamera
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$busy = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -notmatch '-adb2' -and $_.CommandLine -like '*Crest4-URP-Port*'
})
if ($busy.Count) { throw 'Target Editor is already open' }
$stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$run = (New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot "P12-CompiledProbes-$stamp")).FullName
$arguments = @(
    '-batchmode', '-quit', '-projectPath', ('"{0}"' -f $root), '-force-d3d11',
    '-executeMethod', 'Crest.URP.Editor.ResourceShutdownProbeBuild.BuildLocalProbes', '-crestTraceLocalProbes',
    '-logFile', ('"{0}/build.log"' -f $run)
)
$probeFlags = switch ($Probe) {
    'Box' { '-crestProbeBoxOnly' }
    'Atlas' { '-crestProbeAtlasOnly' }
}
$arguments += @($probeFlags | Where-Object { $_ })
$build = Start-Process $Editor -WorkingDirectory $root -WindowStyle Hidden -Wait -PassThru -ArgumentList $arguments
if ($build.ExitCode -ne 0) { throw "Probe build failed ($($build.ExitCode)). See $run" }

$failed = @()
foreach ($api in 'Direct3D11', 'Direct3D12') {
    $directory = (New-Item -ItemType Directory -Path (Join-Path $run $api)).FullName
    $flag = if ($api -eq 'Direct3D11') { '-force-d3d11' } else { '-force-d3d12' }
    $playerArguments = @(
        '-batchmode', $flag, '-crestResourceProbe', '-crestLocalProbes',
        '-crestProbeOutput', ('"{0}/report.json"' -f $directory), '-logFile', ('"{0}/Player.log"' -f $directory)
    )
    $playerArguments += @($probeFlags | Where-Object { $_ })
    if ($AutomaticCamera) {
        $playerArguments = @($playerArguments | Where-Object { $_ -ne '-batchmode' })
        $playerArguments += '-crestProbeAutomaticCamera', '-screen-fullscreen', '0', '-screen-width', '320', '-screen-height', '180'
    }
    $windowStyle = if ($AutomaticCamera) { 'Normal' } else { 'Hidden' }
    $player = Start-Process (Join-Path $root 'Builds/P12-LocalProbes/LocalProbes.exe') -WorkingDirectory $root -WindowStyle $windowStyle -PassThru -ArgumentList $playerArguments
    if (!$player.WaitForExit(90000)) { throw "Probe Player still running (PID $($player.Id)). See $directory" }
    $path = Join-Path $directory 'report.json'
    $report = if (Test-Path -LiteralPath $path) { Get-Content -Raw -LiteralPath $path | ConvertFrom-Json }
    $report | ConvertTo-Json
    if ($player.ExitCode -ne 0 -or !$report.passed -or $report.errors -ne 0 -or $report.api -ne $api) { $failed += $api }
}
if ($failed.Count) { throw "Compiled probes failed on $($failed -join ', '). See $run" }
Write-Output "Compiled probe reports: $run"
