param([switch]$ReuseFixture)
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
 [ordered]@{state='waiting-editor';utc=[DateTime]::UtcNow.ToString('o');editorPids=@($occupied.ProcessId)} | ConvertTo-Json | Set-Content Evidence/P11-Lightmaps-operation.json
 throw 'Target project is open in Unity; preserve user work and close it before a diagnostic build.'
}
$buildMethod=if($ReuseFixture){'Crest.URP.Editor.ResourceShutdownProbeBuild.RebuildLightmapsFromFixture'}else{'Crest.URP.Editor.ResourceShutdownProbeBuild.BuildLightmaps'}
[ordered]@{state='building';utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content Evidence/P11-Lightmaps-operation.json
$p=Start-Process 'C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe' -WindowStyle Hidden -ArgumentList '-batchmode','-projectPath',('"'+(Get-Location).Path+'"'),'-force-d3d11','-executeMethod',$buildMethod,'-logFile',('"'+(Join-Path (Get-Location) 'Evidence/P11-Lightmaps-build.log')+'"') -Wait -PassThru
if($p.ExitCode -ne 0){
 [ordered]@{state='failed';utc=[DateTime]::UtcNow.ToString('o');exitCode=$p.ExitCode} | ConvertTo-Json | Set-Content Evidence/P11-Lightmaps-operation.json
 throw "Lightmap build exit$($p.ExitCode)"
}
[ordered]@{state='build-complete';utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content Evidence/P11-Lightmaps-operation.json
