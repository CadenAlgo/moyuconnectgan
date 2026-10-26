param(
  [string]$Configuration = 'Release',
  [string]$Runtime = 'win-x64',
  [string]$OutputDirectory = 'outputs\GanBridgeProbe-v2',
  [string]$StageDirectory = '.cache\stage-v2-20261024'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'src\GanBridgeProbe\GanBridgeProbe.csproj'
$source = Join-Path $root 'src\GanBridgeProbe'
$output = [System.IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
$stage = [System.IO.Path]::GetFullPath((Join-Path $root $StageDirectory))
$rootPrefix = $root.TrimEnd('\') + '\'
foreach ($path in @($output,$stage)) {
  if (-not $path.StartsWith($rootPrefix,[System.StringComparison]::OrdinalIgnoreCase)) { throw "Path escapes workspace: $path" }
}
$packagePath = Join-Path $output 'GanBridgeProbe.msix'
$certPath = Join-Path $output 'GanBridgeProbe.cer'
$pfxPath = Join-Path $stage 'signing-temporary.pfx'
$windowsKit = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64'
$makeAppx = Join-Path $windowsKit 'MakeAppx.exe'
$signTool = Join-Path $windowsKit 'SignTool.exe'

if (-not (Test-Path $project)) { throw "Project not found: $project" }
if (-not (Test-Path $makeAppx)) { throw "Windows SDK MakeAppx not found: $makeAppx" }
if (-not (Test-Path $signTool)) { throw "Windows SDK SignTool not found: $signTool" }
if (Test-Path $stage) { throw "Staging directory already exists; choose a fresh StageDirectory rather than overwriting it: $stage" }
if (Test-Path $packagePath) { throw "Output package already exists; choose a fresh OutputDirectory rather than overwriting it: $packagePath" }
if (Test-Path $certPath) { throw "Output certificate already exists; choose a fresh OutputDirectory rather than overwriting it: $certPath" }
New-Item -ItemType Directory -Force -Path $output,(Split-Path $stage) | Out-Null
New-Item -ItemType Directory -Force -Path $stage,(Join-Path $stage 'Assets') | Out-Null

Write-Host 'Publishing self-contained Windows app (bundles .NET 8 + Windows Desktop Runtime)…'
dotnet publish $project -c $Configuration -r $Runtime --self-contained true -o $stage
Copy-Item -LiteralPath (Join-Path $source 'Package.appxmanifest') -Destination (Join-Path $stage 'AppxManifest.xml')
Copy-Item -LiteralPath (Join-Path $source 'Assets\Square150x150Logo.png') -Destination (Join-Path $stage 'Assets\Square150x150Logo.png')
Copy-Item -LiteralPath (Join-Path $source 'Assets\Square44x44Logo.png') -Destination (Join-Path $stage 'Assets\Square44x44Logo.png')
& $makeAppx pack /d $stage /p $packagePath
if ($LASTEXITCODE -ne 0) { throw "MakeAppx failed with exit code $LASTEXITCODE" }

$subject = 'CN=MagicubeGanBridgeProbe'
$signingDir = Join-Path $root '.cache\signing'
New-Item -ItemType Directory -Force -Path $signingDir | Out-Null
$pfxPath = Join-Path $signingDir 'GanBridgeProbe.pfx'
$passwordFile = Join-Path $signingDir 'password.txt'
$certPath = Join-Path $signingDir 'GanBridgeProbe.cer'

if ((Test-Path $pfxPath) -and (Test-Path $passwordFile)) {
  $passwordText = (Get-Content -LiteralPath $passwordFile -Raw).Trim()
  Write-Host "Reusing persisted signing certificate: $pfxPath"
} else {
  Write-Host 'Creating a new persistent signing certificate (one-time)…'
  $cert = New-SelfSignedCertificate -Type Custom -Subject $subject -KeyAlgorithm RSA -KeyLength 2048 -KeyUsage DigitalSignature -FriendlyName 'MoYu-GAN BLE Bridge (persistent signing)' -CertStoreLocation 'Cert:\CurrentUser\My' -NotAfter (Get-Date).AddYears(5) -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3')
  $passwordText = [Guid]::NewGuid().ToString('N')
  $password = ConvertTo-SecureString -String $passwordText -AsPlainText -Force
  Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $password | Out-Null
  Set-Content -LiteralPath $passwordFile -Value $passwordText -NoNewline -Encoding ascii
  Export-Certificate -Cert $cert -FilePath $certPath -Type CERT | Out-Null
  Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force -ErrorAction SilentlyContinue
  Write-Host "Persisted signing certificate at: $pfxPath"
}

Copy-Item -LiteralPath $certPath -Destination (Join-Path $output 'GanBridgeProbe.cer') -Force
$password = ConvertTo-SecureString -String $passwordText -AsPlainText -Force
& $signTool sign /fd SHA256 /f $pfxPath /p $passwordText $packagePath
if ($LASTEXITCODE -ne 0) { throw "SignTool failed with exit code $LASTEXITCODE" }

Write-Host "Built: $packagePath"
Write-Host "Public install certificate: $(Join-Path $output 'GanBridgeProbe.cer')"
Write-Host 'The signing key is persisted under .cache\signing so future builds reuse the same trusted certificate.'