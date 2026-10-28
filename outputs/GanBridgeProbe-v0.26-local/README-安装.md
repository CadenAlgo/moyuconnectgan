# Windows v0.26-local install and launch

## On the development PC

Double-click the project root `启动-GAN桥接程序.bat`. It checks the installed package and launches version `1.0.0.26`.

## Upgrade from an older version

You do not need to uninstall the older package. Pull or download the latest repository, then double-click the root `启动-GAN桥接程序.bat`; it detects version `1.0.0.26` and updates the installed MSIX in place.

## On another Windows PC

1. Clone or extract this repository.
2. Open PowerShell at the repository root.
3. Run `Set-Location .\outputs\GanBridgeProbe-v0.26-local`.
4. Run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-Test.ps1`.
5. Approve the Windows UAC prompt to trust the local test certificate.
6. Launch from Start or use the repository-root `启动-GAN桥接程序.bat`.

The MSIX is self-contained and includes the .NET Desktop Runtime. `GanBridgeProbe.cer` is the public certificate needed to install the package. It does not contain a private key. The signing private key and password are not included and are not needed by users.

## Verify package integrity

Compare the MSIX and certificate SHA-256 values with `SHA256SUMS.txt`.
