param([string]$CertificatePath)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $CertificatePath)) { throw "Certificate not found: $CertificatePath" }
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
  $elevatedArguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -CertificatePath `"$CertificatePath`""
  $child = Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $elevatedArguments -Wait -PassThru
  if ($child.ExitCode -ne 0) { throw "Certificate trust setup was cancelled or failed (exit $($child.ExitCode))." }
  exit 0
}
$cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($CertificatePath)
foreach ($store in @('Cert:\LocalMachine\Root','Cert:\LocalMachine\TrustedPeople')) {
  if (-not (Get-ChildItem $store | Where-Object Thumbprint -eq $cert.Thumbprint)) {
    Import-Certificate -FilePath $CertificatePath -CertStoreLocation $store | Out-Null
  }
}
Write-Host 'Temporary app-signing certificate trusted for this local test package.'
