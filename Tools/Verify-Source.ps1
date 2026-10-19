param([Parameter(Mandatory=$true)][string]$Upstream)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot
$source = Join-Path (Resolve-Path $Upstream) 'crest/Assets/Crest'
$destination = Join-Path $project 'Assets/Crest'
$checked = 0
$failures = @()
Get-ChildItem $source -Recurse -File | Where-Object { $_.Extension -in @('.meta','.compute') } | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($source, $_.FullName)
    $target = Join-Path $destination $relative
    if (!(Test-Path -LiteralPath $target)) { $failures += "Missing: $relative" }
    elseif ((Get-FileHash -LiteralPath $_.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
        $failures += "Changed: $relative"
    }
    $checked++
}
$report = [ordered]@{ check='Original meta and compute file byte preservation'; checked=$checked; status=$(if ($failures.Count) {'FAILED'} else {'PASSED'}); failures=$failures; limitation='Does not validate compilation, shader includes, GPU execution, graphics, physics or performance.' }
$report | ConvertTo-Json -Depth 5
if ($failures.Count) { exit 1 }
