@echo off
setlocal
set "APP_NAME=MagicubeGanBridgeProbe"
set "TARGET_VERSION=1.0.0.26"
set "PACKAGE_DIR=%~dp0outputs\GanBridgeProbe-v0.26-local"
set "PACKAGE=%PACKAGE_DIR%\GanBridgeProbe.msix"
set "INSTALLER=%PACKAGE_DIR%\Install-Test.ps1"

echo Starting MoYu-to-GAN BLE bridge v0.26-local...

for /f "usebackq delims=" %%V in (`powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$p=Get-AppxPackage -Name '%APP_NAME%' | Select-Object -First 1; if($p){$p.Version}"`) do set "INSTALLED_VERSION=%%V"

if not "%INSTALLED_VERSION%"=="%TARGET_VERSION%" (
  if not exist "%PACKAGE%" (
    echo ERROR: Missing package: %PACKAGE%
    pause
    exit /b 1
  )
  echo Updating bridge package to %TARGET_VERSION%...
  taskkill /F /IM GanBridgeProbe.exe >nul 2>&1
  powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Add-AppxPackage -Path '%PACKAGE%' -ForceUpdateFromAnyVersion"
  if errorlevel 1 (
    echo Direct package update failed. Trying certificate-aware installer...
    if not exist "%INSTALLER%" (
      echo ERROR: Missing installer: %INSTALLER%
      pause
      exit /b 1
    )
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%INSTALLER%"
    if errorlevel 1 (
      echo ERROR: Package installation failed.
      pause
      exit /b 1
    )
    exit /b 0
  )
)

for /f "usebackq delims=" %%P in (`powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$p=Get-AppxPackage -Name '%APP_NAME%' | Select-Object -First 1; if($p){$p.PackageFamilyName}"`) do set "FAMILY=%%P"

if not defined FAMILY (
  echo ERROR: The bridge app is not installed.
  pause
  exit /b 1
)

start "" explorer.exe "shell:AppsFolder\%FAMILY%!App"
echo Bridge v0.26-local started.
echo Click the GAN broadcast button in the bridge window.

endlocal
