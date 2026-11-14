param([Parameter(Mandatory)][ValidateSet('DX11','DX12')][string]$Api)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$run = (New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot "P11-DebugGuiKeyboard-$Api-$stamp")).FullName
$exe = Join-Path $root 'Builds/P11-DebugGui/DebugGui.exe'
$flag = if ($Api -eq 'DX11') { '-force-d3d11' } else { '-force-d3d12' }
$report = Join-Path $run 'report.json'
$player = Start-Process $exe -WorkingDirectory (Split-Path $exe) -WindowStyle Normal -PassThru -ArgumentList @(
    $flag, '-screen-width', '960', '-screen-height', '540', '-screen-fullscreen', '0',
    '-crestResourceProbe', '-crestDebugGui', '-crestDebugGuiKeyboard',
    '-crestProbeOutput', ('"{0}"' -f $report), '-logFile', ('"{0}/Player.log"' -f $run)
)
Write-Output "Player PID $($player.Id). Press G, G, F, F after the debug panel appears. Report directory: $run"
if (!$player.WaitForExit(165000)) {
    if (!$player.HasExited) { $player.Kill(); $player.WaitForExit() }
    throw "Debug GUI $Api timed out. See $run"
}
$result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
$expectedApi = if ($Api -eq 'DX11') { 'Direct3D11' } else { 'Direct3D12' }
if ($player.ExitCode -ne 0 -or $result.phase -ne 'keyboard-complete' -or $result.errors -ne 0 -or
    $result.keys.Count -ne 4 -or $result.api -ne $expectedApi) {
    throw "Debug GUI $Api failed. See $run"
}
$result.keys | Format-Table
