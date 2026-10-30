param([string]$Prefix = 'P10-Ownership')
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$player = Join-Path $repository 'Builds/P10-ResourceProbe/ResourceProbe.exe'
if (Get-Process Unity,ResourceProbe -ErrorAction SilentlyContinue) { throw 'Close existing Unity/ResourceProbe processes first.' }
if ($Prefix -notmatch '^[A-Za-z0-9_-]+$') { throw 'Invalid evidence prefix.' }
$runs = @()
foreach ($api in @('DX11','DX12')) {
    $cases = @(
        @{ Mode='Empty'; Readback='None'; NoHDR=$false },
        @{ Mode='Empty'; Readback='Full'; NoHDR=$false },
        @{ Mode='Empty'; Readback='Crop'; NoHDR=$false },
        @{ Mode='Empty'; Readback='None'; NoHDR=$true },
        @{ Mode='Ocean'; Readback='Crop'; NoHDR=$false }
    )
    foreach ($case in $cases) {
        $name = "$Prefix-$($case.Mode)-$($case.Readback)"
        if ($case.NoHDR) { $name += '-NoHDR' }
        $name += "-$api"
        $report = Join-Path $repository "Evidence/$name.json"
        $log = Join-Path $repository "Evidence/$name.log"
        if ((Test-Path -LiteralPath $report) -or (Test-Path -LiteralPath $log)) { throw "Evidence already exists: $name" }
        $arguments = @('-crestResourceProbe','-crestOwnership','-crestOwnershipReadback',$case.Readback,
            '-crestProbeMode',$case.Mode,'-screen-fullscreen','0','-screen-width','960','-screen-height','540',
            "-force-d3d$($api.Substring(2))",'-crestProbeOutput',('"'+$report+'"'),'-logFile',('"'+$log+'"'))
        if ($case.Mode -eq 'Empty') { $arguments += '-crestProbeDisableFeature' }
        if ($case.NoHDR) { $arguments += '-crestOwnershipNoHDR' }
        $start = [DateTime]::UtcNow.ToString('o')
        $process = Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Hidden -PassThru
        $timedOut = -not $process.WaitForExit(45000)
        if ($timedOut) { $process.Kill(); $process.WaitForExit() }
        $process.Refresh()
        $runs += [ordered]@{ name=$name; pid=$process.Id; startUtc=$start; endUtc=[DateTime]::UtcNow.ToString('o');
            exitCode=$process.ExitCode; timedOut=$timedOut; arguments=$arguments; reportExists=(Test-Path -LiteralPath $report) }
        $runs | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $repository "Evidence/$Prefix-runs.json") -Encoding utf8
        Write-Output "$name exit=$($process.ExitCode) timeout=$timedOut"
    }
}
if ($runs | Where-Object { $_.timedOut -or $_.exitCode -ne 0 -or -not $_.reportExists }) { throw 'At least one case failed; inspect retained evidence.' }
