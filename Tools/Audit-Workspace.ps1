param([Parameter(Mandatory=$true)][string]$Upstream)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot))
$original = Join-Path (Resolve-Path $Upstream) 'crest/Assets/Crest'
$files = @()
foreach ($folder in @('Assets','Packages','ProjectSettings','Tools')) {
    $files += Get-ChildItem -LiteralPath (Join-Path $project $folder) -Recurse -File
}
$files += Get-ChildItem -LiteralPath $project -File
$inventory = @()
$missingMeta = @()
$guids = @{}
foreach ($file in $files) {
    $relative = [IO.Path]::GetRelativePath($project, $file.FullName).Replace('\','/')
    $inventory += [ordered]@{
        path=$relative; bytes=$file.Length
        sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
    if ($relative.StartsWith('Assets/')) {
        if ($file.Extension -eq '.meta') {
            $match = [regex]::Match([IO.File]::ReadAllText($file.FullName), '(?m)^guid: ([a-f0-9]{32})')
            if ($match.Success) {
                $guid = $match.Groups[1].Value
                if (!$guids.ContainsKey($guid)) { $guids[$guid] = @() }
                $guids[$guid] += $relative
            }
        } elseif (!(Test-Path -LiteralPath ($file.FullName + '.meta'))) {
            $missingMeta += $relative
        }
    }
}
$duplicates = @($guids.Keys | Where-Object { $guids[$_].Count -gt 1 } | ForEach-Object {
    [ordered]@{guid=$_; paths=$guids[$_]}
})
$missingOriginal = @()
$changedOriginal = @()
Get-ChildItem -LiteralPath $original -Recurse -File | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($original, $_.FullName)
    $target = Join-Path (Join-Path $project 'Assets/Crest') $relative
    if (!(Test-Path -LiteralPath $target)) { $missingOriginal += $relative }
    elseif ((Get-FileHash -LiteralPath $_.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
        $changedOriginal += $relative
    }
}
$testReports = @(Get-ChildItem -LiteralPath (Join-Path $project 'Evidence') -Filter 'EditMode-*.xml' | ForEach-Object {
    [xml]$xml = Get-Content -Raw -LiteralPath $_.FullName
    $run = $xml.'test-run'
    [ordered]@{file=$_.Name; start=$run.'start-time'; result=$run.result; total=$run.total; passed=$run.passed; failed=$run.failed}
})
$report = [ordered]@{
    recordedUtc=[DateTime]::UtcNow.ToString('o')
    scope='All files in Assets, Packages, ProjectSettings, Tools, and repository root; generated Library/Builds and historical work copies excluded.'
    limitation='Inventory and structural checks, not a line-by-line semantic review or functional acceptance. Changed upstream files are disclosed, not automatically failures.'
    fileCount=$inventory.Count
    missingAssetMeta=$missingMeta
    duplicateGuids=$duplicates
    missingOriginalFiles=$missingOriginal
    changedOriginalFiles=$changedOriginal
    currentTestReports=$testReports
    files=$inventory
}
$destination = Join-Path $project 'Evidence/workspace-audit.json'
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $destination
Write-Output "Audited $($inventory.Count) files; missing meta=$($missingMeta.Count), duplicate GUIDs=$($duplicates.Count), missing upstream files=$($missingOriginal.Count)."
Write-Output "Report: $destination"
if ($missingMeta.Count -or $duplicates.Count -or $missingOriginal.Count) { exit 1 }
