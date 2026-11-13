param([string]$Prefix = 'P11-Final')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location -LiteralPath $root
$editor = 'C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe'
$operation = "Evidence/$Prefix-operation.json"
$history = [Collections.Generic.List[object]]::new()
$materialPaths = @(
    'Assets/Crest/Crest-Examples/Examples/Materials/Examples_Material_TransparentPrimitive.mat',
    'Assets/Crest/Crest-Examples/Shared/Materials/OffWhite.mat'
)

function Save-State($step, $state, $errorMessage = '') {
    [ordered]@{ step=$step; state=$state; utc=[DateTime]::UtcNow.ToString('o'); history=$history; error=$errorMessage } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $operation
}

function Invoke-Step([string]$name, [scriptblock]$action) {
    $target = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
        $_.CommandLine -notmatch '-adb2' -and $_.CommandLine -like '*Crest4-URP-Port*'
    })
    if ($target.Count) { throw "Target Editor already open before $name." }
    $start = [DateTime]::UtcNow
    Save-State $name running
    & $action
    $history.Add([ordered]@{step=$name; startedUtc=$start.ToString('o'); endedUtc=[DateTime]::UtcNow.ToString('o')})
    Save-State $name complete
}

try {
    foreach ($api in @('DX11','DX12')) {
        Invoke-Step "Full $api" { & ./Tools/Run-Unity.ps1 -Editor $editor -Action EditMode -GraphicsAPI $api }
    }
    foreach ($api in @('DX11','DX12')) {
        Invoke-Step "Interactive $api" { & ./Tools/Run-Unity.ps1 -Editor $editor -Action EditMode -GraphicsAPI $api -Interactive }
    }
    Invoke-Step 'Restore known fixture material normalization' {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $baseline = [IO.Compression.ZipFile]::OpenRead((Resolve-Path "Evidence/$Prefix-before.zip"))
        $known = [IO.Compression.ZipFile]::OpenRead((Resolve-Path Evidence/P11-Examples-test-mutated-materials.zip))
        try {
            foreach ($path in $materialPaths) {
                $current = (Get-FileHash -LiteralPath $path).Hash
                $entry = $baseline.GetEntry($path)
                $sha = [Security.Cryptography.SHA256]::Create()
                try {
                    $stream = $entry.Open()
                    try { $original = [Convert]::ToHexString($sha.ComputeHash($stream)) } finally { $stream.Dispose() }
                    if ($current -eq $original) { continue }
                    $stream = $known.GetEntry($path).Open()
                    try { $expected = [Convert]::ToHexString($sha.ComputeHash($stream)) } finally { $stream.Dispose() }
                    if ($current -ne $expected) { throw "Unexpected test mutation in $path. Preserve and inspect." }
                    $archive = Join-Path $PSScriptRoot ("$Prefix-mutated-" + (Split-Path $path -Leaf))
                    Copy-Item -LiteralPath $path -Destination $archive
                    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Resolve-Path -LiteralPath $path), $true)
                } finally { $sha.Dispose() }
            }
        } finally {
            $baseline.Dispose()
            $known.Dispose()
        }
    }
    Invoke-Step ValidateAssets { & ./Tools/Run-Unity.ps1 -Editor $editor -Action ValidateAssets }
    foreach ($label in @('Development','Release')) {
        Invoke-Step "Build $label" { & ./Tools/Run-Unity.ps1 -Editor $editor -Action $label }
    }
    foreach ($label in @('Development','Release')) {
        foreach ($api in @('DX11','DX12')) {
            Invoke-Step "Smoke $label $api" {
                & ./Tools/Run-Player.ps1 -Player "Builds/$label/CrestPort.exe" -GraphicsAPI $api -Label $label -ReflectionOptions
            }
        }
    }
    Save-State 'All commands' complete
} catch {
    $last = Get-Content -LiteralPath $operation -Raw -ErrorAction SilentlyContinue | ConvertFrom-Json
    Save-State $last.step failed $_.Exception.Message
    throw
}
