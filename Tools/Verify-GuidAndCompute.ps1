param([Parameter(Mandatory=$true)][string]$Upstream)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot
$source = Join-Path (Resolve-Path $Upstream) 'crest/Assets/Crest'
$destination = Join-Path $project 'Assets/Crest'
$failures = @()
$normalizedMetadata = @()
$checked = 0
Get-ChildItem $source -Recurse -File | Where-Object { $_.Extension -in @('.meta','.compute') } | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($source, $_.FullName)
    $target = Join-Path $destination $relative
    if (!(Test-Path -LiteralPath $target)) { $failures += "Missing: $relative" }
    elseif ($_.Extension -eq '.compute') {
        if ((Get-FileHash -LiteralPath $_.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
            $failures += "Compute changed: $relative"
        }
    }
    else {
        $original = [IO.File]::ReadAllText($_.FullName)
        $current = [IO.File]::ReadAllText($target)
        $originalGuid = [regex]::Match($original, '(?m)^guid: ([a-f0-9]{32})\s*$').Groups[1].Value
        $currentGuid = [regex]::Match($current, '(?m)^guid: ([a-f0-9]{32})\s*$').Groups[1].Value
        if (!$originalGuid -or $originalGuid -ne $currentGuid) { $failures += "GUID changed: $relative" }
        if ((Get-FileHash -LiteralPath $_.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
            $normalizedMetadata += $relative
        }
    }
    $checked++
}
$report = [ordered]@{
    check='Original asset GUIDs and compute shader bytes'
    checked=$checked
    status=$(if ($failures.Count) {'FAILED'} else {'PASSED'})
    failures=$failures
    changedMetadata=$normalizedMetadata
    limitation='Metadata changes are reported, not classified as byte-identical. Run Verify-Source.ps1 for the separate strict byte-preservation check. Neither check establishes feature acceptance.'
}
$report | ConvertTo-Json -Depth 5
if ($failures.Count) { exit 1 }
