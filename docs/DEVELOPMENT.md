# Development notes

## Current verified state

- Current package: `outputs/GanBridgeProbe-v0.24-local/GanBridgeProbe.msix`, identity version `1.0.0.24`.
- The user confirmed on 2026-10-27 that the full setup works in practice.
- Windows device name should begin with `GAN` so Cube Station can discover it; keep it short because the GATT service UUID and name share a limited advertisement payload.
- Current tested phone app: Magicube / CubeStation (魔方星球) Android 6.8.
- Current tested source cube: MoYu V10 AI.
- GAN AES salt is the BLE address observed for the active virtual device; read it from nRF Connect after each fresh advertisement if Windows rotates its private address, then apply it in the `GAN MAC` input before reconnecting CubeStation.

## Architecture

`Program.cs` owns the Windows MSIX GATT Peripheral and Central BLE flows. `MoYu/MoYuProtocol.cs` decodes MoYu A1/A3/A4/A5 frames. `GanGen2/` builds/encrypts GAN Gen2 notifications. `CubeState.cs` maintains cubie state. `BridgeSelfTests.cs` runs protocol sanity checks at app startup.

## Build environment

- Local SDK: `.tools/dotnet` (.NET 8).
- NuGet cache: `.cache/nuget`.
- Local package signing key and password: `.cache/signing/` (never commit).
- Reference checkouts and reverse-engineering artifacts under `.cache/` are local-only and ignored.
- Use a fresh output and stage directory because `src/build-package.ps1` refuses to overwrite existing build folders.

## Known constraints

- Windows controls the BLE private address and the GattServiceProvider advertisement layout.
- Windows reserves the standard Device Information Service (0x180A) for its system implementation; attempting to create it returns `DisabledByPolicy`.
- Keep the one-click launcher ASCII-only to avoid Windows cmd encoding issues.
- Do not remove `.tools/dotnet`, `.cache/nuget`, or `.cache/signing` when cleaning old build output.
