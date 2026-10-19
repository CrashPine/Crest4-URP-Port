param(
    [Parameter(Mandatory=$true)][string]$Editor,
    [ValidateSet('Import','Generate','ValidateAssets','EditMode','PlayMode','Development','Release')][string]$Action='Import',
    [ValidateSet('DX11','DX12')][string]$GraphicsAPI='DX11'
)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot
$version = (Get-Item -LiteralPath $Editor).VersionInfo.ProductVersion
if ($version -notmatch '^6000\.6\.5') { throw "Target validation requires Unity 6000.6.5f1, received $version" }
$evidence = Join-Path $project 'Evidence'
New-Item -ItemType Directory -Force $evidence | Out-Null
$arguments = @('-batchmode','-projectPath',('"'+$project+'"'),'-logFile',('"'+(Join-Path $evidence "$Action-$GraphicsAPI.log")+'"'))
$arguments += $(if ($GraphicsAPI -eq 'DX12') {'-force-d3d12'} else {'-force-d3d11'})
switch ($Action) {
 'Generate' { $arguments += @('-quit','-executeMethod','Crest.URP.Editor.PortAutomation.GenerateScenes') }
 'ValidateAssets' { $arguments += @('-quit','-executeMethod','Crest.URP.Editor.PortAutomation.ValidateAssets') }
 'Development' { $arguments += @('-quit','-executeMethod','Crest.URP.Editor.PortAutomation.BuildDevelopment') }
 'Release' { $arguments += @('-quit','-executeMethod','Crest.URP.Editor.PortAutomation.BuildRelease') }
 'EditMode' { $arguments += @('-runTests','-testPlatform','EditMode','-testResults',('"'+(Join-Path $evidence "EditMode-$GraphicsAPI.xml")+'"')) }
 'PlayMode' { $arguments += @('-runTests','-testPlatform','PlayMode','-testResults',('"'+(Join-Path $evidence "PlayMode-$GraphicsAPI.xml")+'"')) }
 default { $arguments += '-quit' }
}
$process = Start-Process -FilePath $Editor -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Unity exited with $($process.ExitCode); inspect Evidence logs." }
Write-Output 'Unity command exited successfully. This alone does not establish visual correctness or feature acceptance.'
