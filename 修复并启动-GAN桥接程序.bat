@echo off
setlocal EnableExtensions
title Repair and Start MoYu-to-GAN BLE Bridge

set "SCRIPT="
for /f "usebackq delims=" %%S in (
  `powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$items=@(Get-ChildItem -LiteralPath '%~dp0' -Filter '*GAN*.ps1' -File); if($items.Count -eq 1){$items[0].FullName} else {exit 2}"`
) do set "SCRIPT=%%S"

if not defined SCRIPT (
  echo ERROR: Could not find exactly one GAN recovery PowerShell script in this folder.
  echo Expected a file matching *GAN*.ps1 beside this BAT file.
  echo.
  pause
  exit /b 1
)

echo Starting Bluetooth recovery. Windows will ask for administrator permission.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%"
set "EXIT_CODE=%ERRORLEVEL%"

if not "%EXIT_CODE%"=="0" (
  echo.
  echo ERROR: Bluetooth recovery failed. Exit code: %EXIT_CODE%
  echo Check the message above, then restart Windows if necessary.
  pause
  exit /b %EXIT_CODE%
)

endlocal
exit /b 0
