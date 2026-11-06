$ErrorActionPreference='Stop'
& ./Tools/Run-Unity.ps1 -Editor 'C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe' -Action EditMode -GraphicsAPI DX11 -TestFilter SurfaceInputTests.FreshSphereClipMaterialSelectsItsRequiredVariant
Copy-Item Evidence/EditMode-DX11-focused.xml Evidence/P11-Examples-sphere-focused-DX11.xml
Copy-Item Evidence/EditMode-DX11-focused.log Evidence/P11-Examples-sphere-focused-DX11.log
