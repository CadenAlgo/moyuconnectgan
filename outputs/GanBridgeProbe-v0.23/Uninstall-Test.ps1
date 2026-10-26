$ErrorActionPreference = 'Continue'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$certificate = Join-Path $here 'GanBridgeProbe.cer'
$removeHelper = Join-Path $here 'Remove-TestCertificate.ps1'
Get-AppxPackage -Name 'MagicubeGanBridgeProbe' | Remove-AppxPackage
if (Test-Path $certificate) {
  $cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certificate)
  & $removeHelper -Thumbprint $cert.Thumbprint
  foreach ($store in @('Cert:\CurrentUser\TrustedPeople','Cert:\CurrentUser\Root','Cert:\CurrentUser\My')) {
    Get-ChildItem $store -ErrorAction SilentlyContinue | Where-Object Thumbprint -eq $cert.Thumbprint | Remove-Item -Force -ErrorAction SilentlyContinue
  }
}
Write-Host 'Probe app and its local test signing certificate have been removed.'
