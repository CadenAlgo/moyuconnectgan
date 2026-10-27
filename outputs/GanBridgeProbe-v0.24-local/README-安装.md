# Windows v0.24-local install and launch

## On the development PC

Double-click the project root `启动-GAN桥接程序.bat`. It checks the installed package and launches version `1.0.0.24`.

## On another Windows PC

1. Clone or extract this repository.
2. Open PowerShell in this folder and run the root README's first-install command with `Install-Test.ps1`.
3. Approve the Windows UAC prompt to trust the local test certificate.
4. Launch from Start or use the root BAT file.

The MSIX is self-contained and includes the .NET Desktop Runtime. `GanBridgeProbe.cer` is the public certificate needed to install the package. The signing private key and password are not included.

## Verify package integrity

Compare the MSIX and certificate SHA-256 values with `SHA256SUMS.txt`.
