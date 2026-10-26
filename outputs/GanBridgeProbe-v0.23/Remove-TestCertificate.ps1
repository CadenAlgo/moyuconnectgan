param([string]$Thumbprint)
$ErrorActionPreference = 'Stop'
if (-not $Thumbprint) { throw 'Thumbprint is required.' }
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
  $elevatedArguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Thumbprint `"$Thumbprint`""
  $child = Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $elevatedArguments -Wait -PassThru
  if ($child.ExitCode -ne 0) { throw "Certificate removal was cancelled or failed (exit $($child.ExitCode))." }
  exit 0
}
foreach ($store in @('Cert:\LocalMachine\TrustedPeople','Cert:\LocalMachine\Root')) {
  Get-ChildItem $store | Where-Object Thumbprint -eq $Thumbprint | Remove-Item -Force
}
Write-Host 'Temporary test package certificate removed from LocalMachine stores.'
