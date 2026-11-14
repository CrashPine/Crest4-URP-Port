$ErrorActionPreference='Stop'
$path='Evidence/P11-Current-operation.json'
$s=Get-Content $path -Raw|ConvertFrom-Json
if($s.state -ne 'failed' -or $s.step -ne 'Smoke Release DX11' -or $s.history.Count -ne 10){throw 'Unexpected checkpoint'}
foreach($api in 'DX11','DX12'){
 $s.step="Smoke Release $api"
 $s.state='running'
 $s.error=''
 $s.utc=[DateTime]::UtcNow.ToString('o')
 $s|ConvertTo-Json -Depth 8|Set-Content $path
 try {
  & ./Tools/Run-Player.ps1 -Player Builds/Release/CrestPort.exe -GraphicsAPI $api -Label Release -ReflectionOptions
 } catch {
  $s.state='failed'
  $s.error=$_.Exception.Message
  $s|ConvertTo-Json -Depth 8|Set-Content $path
  throw
 }
 $s.history+=@{step=$s.step;startedUtc=$s.utc;endedUtc=[DateTime]::UtcNow.ToString('o')}
}
$s.step='All commands'
$s.state='complete'
$s.utc=[DateTime]::UtcNow.ToString('o')
$s|ConvertTo-Json -Depth 8|Set-Content $path