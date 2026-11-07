$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\','/')
$occupied=@(Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'Unity.exe' -and $_.CommandLine -notmatch '-adb2' } | Where-Object {
 $projectMatch=[regex]::Match($_.CommandLine,'(?i)-projectpath\s+(?:"([^"]+)"|(\S+))')
 if($projectMatch.Success){
  $candidate=$projectMatch.Groups[1].Value;if(!$candidate){$candidate=$projectMatch.Groups[2].Value}
  [IO.Path]::GetFullPath($candidate).TrimEnd('\','/') -eq $projectRoot
 }else{$false}
})
if($occupied.Count){
 [ordered]@{state='waiting-editor';utc=[DateTime]::UtcNow.ToString('o');editorPids=@($occupied.ProcessId)} | ConvertTo-Json | Set-Content Evidence/P11-SavedInputs-operation.json
 throw 'Target project is open in Unity; preserve user work and close it before a diagnostic build.'
}
[ordered]@{state='building';utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content Evidence/P11-SavedInputs-operation.json
$p=Start-Process 'C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe' -WindowStyle Hidden -ArgumentList '-batchmode','-quit','-projectPath',('"'+(Get-Location).Path+'"'),'-force-d3d11','-executeMethod','Crest.URP.Editor.ResourceShutdownProbeBuild.BuildSavedInputs','-logFile',('"'+(Join-Path (Get-Location) 'Evidence/P11-SavedInputs-build.log')+'"') -Wait -PassThru
if($p.ExitCode -ne 0){
 [ordered]@{state='failed';utc=[DateTime]::UtcNow.ToString('o');exitCode=$p.ExitCode} | ConvertTo-Json | Set-Content Evidence/P11-SavedInputs-operation.json
 throw "Saved inputs build exit$($p.ExitCode)"
}
[ordered]@{state='build-complete';utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content Evidence/P11-SavedInputs-operation.json
