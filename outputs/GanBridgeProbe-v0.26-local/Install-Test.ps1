$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$package = Join-Path $here 'GanBridgeProbe.msix'
$certificate = Join-Path $here 'GanBridgeProbe.cer'
$trustHelper = Join-Path $here 'Trust-TestCertificate.ps1'
if (-not (Test-Path $package)) { throw "Missing package: $package" }
if (-not (Test-Path $certificate)) { throw "Missing certificate: $certificate" }
Write-Host 'A Windows UAC prompt will ask to trust this locally generated test package certificate.'
& $trustHelper -CertificatePath $certificate
Add-AppxPackage -Path $package -ForceUpdateFromAnyVersion
$installed = Get-AppxPackage -Name 'MagicubeGanBridgeProbe' | Select-Object -First 1
if (-not $installed) { throw 'Windows did not report the app as installed.' }
$target = "shell:AppsFolder\$($installed.PackageFamilyName)!App"
Start-Process explorer.exe -ArgumentList $target
Write-Host 'Probe v0.26 installed and launched. .NET runtime is bundled; no separate .NET install is needed.'
