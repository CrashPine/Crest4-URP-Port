param([switch]$SkipAcceptedMainDX11)
$ErrorActionPreference='Stop'
try {
 foreach($api in @('DX11','DX12')) {
  foreach($case in @('input','main','boat','threeboats','wakes-off','wakes-on','wakes-off-repeat','rivers','whirl-combined','whirl-isolated')) {
   if($SkipAcceptedMainDX11 -and $api -eq 'DX11' -and $case -eq 'main'){continue} # Accepted once on this exact build.
   [ordered]@{state='running';api=$api;case=$case;utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content Evidence/P11-Scenes-batch-operation.json
   & ./Evidence/P11-Scenes-run.ps1 -API $api -Case $case
  }
 }
 [ordered]@{state='complete';utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content Evidence/P11-Scenes-batch-operation.json
} catch {
 [ordered]@{state='failed';utc=[DateTime]::UtcNow.ToString('o');error=$_.ToString()} | ConvertTo-Json | Set-Content Evidence/P11-Scenes-batch-operation.json
 throw
}
