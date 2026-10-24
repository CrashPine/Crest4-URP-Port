param(
    [Parameter(Mandatory=$true)][string]$Editor,
    [ValidateSet('Import','Generate','ValidateAssets','EditMode','PlayMode','Development','Release')][string]$Action='Import',
    [ValidateSet('DX11','DX12')][string]$GraphicsAPI='DX11',
    [string]$TestFilter='',
    [switch]$Interactive
)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot
$version = (Get-Item -LiteralPath $Editor).VersionInfo.ProductVersion
if ($version -notmatch '^6000\.6\.5') { throw "Target validation requires Unity 6000.6.5f1, received $version" }
$evidence = Join-Path $project 'Evidence'
New-Item -ItemType Directory -Force $evidence | Out-Null
if ($TestFilter -and $Action -notin @('EditMode','PlayMode')) { throw 'TestFilter requires a test action.' }
if ($Interactive -and $Action -ne 'EditMode') { throw 'Interactive currently supports the separate Editor lifecycle test assembly only.' }
$runName = "$Action-$GraphicsAPI" + $(if ($TestFilter) { '-focused' } else { '' }) + $(if ($Interactive) { '-interactive' } else { '' })
$arguments = @('-projectPath',('"'+$project+'"'),'-logFile',('"'+(Join-Path $evidence "$runName.log")+'"'))
if (!$Interactive) { $arguments = @('-batchmode') + $arguments }
if ($Action -eq 'EditMode') { $arguments += @('-assemblyNames', $(if ($Interactive) {'Crest.URP.Interactive.Tests'} else {'Crest.URP.Tests'})) }
$arguments += $(if ($GraphicsAPI -eq 'DX12') {'-force-d3d12'} else {'-force-d3d11'})
switch ($Action) {
 'Generate' { $arguments += @('-quit','-executeMethod','Crest.URP.Editor.PortAutomation.GenerateScenes') }
 'ValidateAssets' { $arguments += @('-quit','-executeMethod','Crest.URP.Editor.PortAutomation.ValidateAssets') }
 'Development' { $arguments += @('-quit','-executeMethod','Crest.URP.Editor.PortAutomation.BuildDevelopment') }
 'Release' { $arguments += @('-quit','-executeMethod','Crest.URP.Editor.PortAutomation.BuildRelease') }
 'EditMode' { $arguments += @('-runTests','-testPlatform','EditMode','-testResults',('"'+(Join-Path $evidence "$runName.xml")+'"')) }
 'PlayMode' { $arguments += @('-runTests','-testPlatform','PlayMode','-testResults',('"'+(Join-Path $evidence "$runName.xml")+'"')) }
 default { $arguments += '-quit' }
}
if ($TestFilter) { $arguments += @('-testFilter', ('"'+$TestFilter+'"')) }
$startedUtc = [DateTime]::UtcNow
$process = Start-Process -FilePath $Editor -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Unity exited with $($process.ExitCode); inspect Evidence logs." }
if ($Action -in @('EditMode','PlayMode')) {
    $resultsPath = Join-Path $evidence "$runName.xml"
    if (!(Test-Path -LiteralPath $resultsPath) -or (Get-Item -LiteralPath $resultsPath).LastWriteTimeUtc -lt $startedUtc) {
        throw 'Unity produced no fresh test report. An earlier report is not evidence for this run.'
    }
    [xml]$results = Get-Content -Raw -LiteralPath $resultsPath
    $run = $results.'test-run'
    if ($run.result -ne 'Passed' -or [int]$run.total -lt 1 -or [int]$run.failed -ne 0 -or [int]$run.skipped -ne 0) {
        throw "Tests not fully passed: result=$($run.result), total=$($run.total), failed=$($run.failed), skipped=$($run.skipped)"
    }
    Write-Output "$($run.passed)/$($run.total) tests passed on $GraphicsAPI."
}
Write-Output 'Unity command exited successfully. This alone does not establish visual correctness or feature acceptance.'
