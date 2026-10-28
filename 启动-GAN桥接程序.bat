@echo off
setlocal
set "APP_NAME=MagicubeGanBridgeProbe"
set "TARGET_VERSION=1.0.0.25"
set "PACKAGE_DIR=%~dp0outputs\GanBridgeProbe-v0.25-local"
set "PACKAGE=%PACKAGE_DIR%\GanBridgeProbe.msix"

echo Starting MoYu-to-GAN BLE bridge v0.25-local...

for /f "usebackq delims=" %%V in (`powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$p=Get-AppxPackage -Name '%APP_NAME%' | Select-Object -First 1; if($p){$p.Version}"`) do set "INSTALLED_VERSION=%%V"

if not "%INSTALLED_VERSION%"=="%TARGET_VERSION%" (
  if not exist "%PACKAGE%" (
    echo ERROR: Missing package: %PACKAGE%
    pause
    exit /b 1
  )
  echo Installing bridge package %TARGET_VERSION%...
  powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Add-AppxPackage -Path '%PACKAGE%' -ForceUpdateFromAnyVersion"
  if errorlevel 1 (
    echo ERROR: Package installation failed.
    pause
    exit /b 1
  )
)

for /f "usebackq delims=" %%P in (`powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$p=Get-AppxPackage -Name '%APP_NAME%' | Select-Object -First 1; if($p){$p.PackageFamilyName}"`) do set "FAMILY=%%P"

if not defined FAMILY (
  echo ERROR: The bridge app is not installed.
  pause
  exit /b 1
)

start "" explorer.exe "shell:AppsFolder\%FAMILY%!App"
echo Bridge v0.25-local started.
echo Click the GAN broadcast button in the bridge window.

endlocal
