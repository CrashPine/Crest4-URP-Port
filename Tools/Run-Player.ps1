param(
    [Parameter(Mandatory=$true)][string]$Player,
    [ValidateSet('DX11','DX12')][string]$GraphicsAPI='DX11',
    [string]$Label='Development',
    [switch]$ReflectionOptions
)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot
$evidence=Join-Path $project ('Evidence/Player-'+$Label+'-'+$GraphicsAPI)
New-Item -ItemType Directory -Force $evidence | Out-Null
$arguments=@('-batchmode','-crestSmoke','-screen-width','960','-screen-height','540','-crestEvidence',('"'+$evidence+'"'),'-logFile',('"'+(Join-Path $evidence 'Player.log')+'"'))
$arguments += $(if($GraphicsAPI -eq 'DX12'){'-force-d3d12'}else{'-force-d3d11'})
if($ReflectionOptions) { $arguments += '-crestReflectionOptions' }
$startedUtc=[DateTime]::UtcNow
$run=Start-Process -FilePath (Resolve-Path $Player) -ArgumentList $arguments -WindowStyle Hidden -PassThru
if(!$run.WaitForExit(180000)) { Stop-Process -Id $run.Id; throw 'Player smoke run exceeded 180 seconds.' }
if($run.ExitCode -ne 0) { throw "Player smoke exited with $($run.ExitCode). Inspect $evidence" }
if(!(Test-Path (Join-Path $evidence 'player-smoke.txt'))) { throw 'Player produced no smoke report.' }
$report=Get-Item -LiteralPath (Join-Path $evidence 'player-smoke.txt')
if($report.LastWriteTimeUtc -lt $startedUtc) { throw 'Player report is stale.' }
if((Get-Content -Raw -LiteralPath $report.FullName) -notmatch '(?m)^Errors=0\.') { throw 'Player report contains smoke failures.' }
$images=@(Get-ChildItem $evidence -Filter Ocean_*.png)
if($images.Count -ne 7) { throw "Expected 7 scene captures; received $($images.Count)." }
if(@($images | Where-Object { $_.LastWriteTimeUtc -lt $startedUtc -or $_.Length -eq 0 }).Count) {
    throw 'One or more scene captures are stale or empty.'
}
Get-Content (Join-Path $evidence 'player-smoke.txt')
if($ReflectionOptions) {
    $optionsReport=Get-Item -LiteralPath (Join-Path $evidence 'reflection-options.txt')
    $optionsImages=@(Get-ChildItem $evidence -Filter ReflectionOptions-*.png)
    if($optionsReport.LastWriteTimeUtc -lt $startedUtc -or $optionsImages.Count -ne 4 -or
       @($optionsImages | Where-Object { $_.LastWriteTimeUtc -lt $startedUtc -or $_.Length -eq 0 }).Count) {
        throw 'Reflection options evidence is incomplete or stale.'
    }
}
