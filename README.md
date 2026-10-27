# MoYu → GAN BLE Bridge（Windows）

> **当前已验证版本**：把魔域 V10 AI 的 BLE 数据转换为 GAN Gen2 风格 GATT 数据，使手机上的“魔方星球（CubeStation）”把 Windows 电脑当作 GAN 智能魔方。项目仍在迭代；当前提供的是本地测试签名包，尚非 Microsoft Store 正式发行。

## 当前验证状态

截至 **2026-10-27**，用户已在实际设备上确认完整流程可用：

| 项目 | 当前测试环境 |
|---|---|
| 电脑 | Windows 11 x64 |
| 电脑 BLE 名称 | `GAN356IC` |
| 蓝牙 | BLE 适配器支持 Central + Peripheral 角色 |
| 实体魔方 | 魔域 V10 AI |
| 手机 | Android 手机 |
| 手机 App | 魔方星球（CubeStation）Android 6.8 |
| 当前 Windows 包 | `1.0.0.24`，MSIX 自包含 .NET 运行时 |

已验证的数据链路：

```text
魔域 V10 AI
    │ BLE Central：扫描、连接、订阅通知
    ▼
Windows 桥接程序（MoYu A1/A3/A4/A5 → GAN Gen2）
    │ BLE Peripheral：GAN GATT 服务和加密通知
    ▼
手机魔方星球 / CubeStation
```

已完成：魔域 V10 AI 解密与状态/电量/转动读取、GAN Gen2 GATT 服务、GAN AES 加密、状态与转动通知、转动计数和实体/虚拟 CubeState 同步。用户确认实体魔方转动可在手机 App 中体现。

## 快速使用（已安装本机）

1. 双击项目根目录的 `启动-GAN桥接程序.bat`。
2. 确认窗口标题为 **“魔域 → GAN BLE 桥接验证器 v0.24-local”**。BAT 会检查 MSIX 版本；如果本机不是 `1.0.0.24`，会从 `outputs/GanBridgeProbe-v0.24-local/` 安装这一版本。
3. 点击 **“诊断：启动 GAN 广播”**，等待日志出现 `GAN GATT 广播成功`。
4. 用手机 nRF Connect 扫描设备 `GAN356IC`，确认其服务 UUID 是 `6e400001-b5a3-f393-e0a9-e50e24dc4179`。记下这次扫描显示的地址；只需扫描读地址，不要让 nRF Connect 保持连接。
5. 在 Windows 程序的 **“GAN MAC（手机看到的地址）”** 输入框填入该地址，点击 **“应用 GAN MAC”**。
6. 完全退出 nRF Connect；完全退出并重新打开魔方星球，扫描并连接 `GAN356IC`。
7. 电脑日志出现 `远端通知订阅变化 ... Count=1` 后，在桥接程序点击 **“扫描魔域魔方”**。
8. 唤醒魔域 V10 AI，在列表中选中对应设备后点击 **“连接选中的魔方”**。等到日志显示魔域服务连接、通知订阅和面状态同步成功。
9. 转动实体魔方，检查魔方星球中的 GAN 魔方是否跟随转动。

### 每次连接的关键点

**BLE 广播地址可能会改变。** 重启电脑、重新开桥接程序或重新开广播后，用 nRF Connect 重新读取 `GAN356IC` 当前显示的地址，并在桥接程序应用该地址，再重新连接魔方星球。不要长期沿用旧地址，也不要把 Windows 的蓝牙适配器 MAC 当作广播 MAC。

CubeStation 的魔方连接和桥接程序连接实体魔域是两个独立 BLE 链路。测试时先让手机连接 GAN 虚拟魔方，再由电脑连接实体魔域魔方；不要让魔域官方 App 同时连接实体魔方。

## 首次安装（新电脑）

交付文件在 `outputs/GanBridgeProbe-v0.24-local/`：

- `GanBridgeProbe.msix`：Windows 桥接程序；
- `GanBridgeProbe.cer`：本地测试包的**公钥证书**，不含签名私钥；
- `Install-Test.ps1` / `Trust-TestCertificate.ps1`：信任证书并安装；
- `Uninstall-Test.ps1` / `Remove-TestCertificate.ps1`：可选卸载工具。

在 Windows PowerShell 中进入该目录并运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-Test.ps1
```

首次信任本地测试证书时，Windows 会请求管理员权限。完成后可以双击项目根目录 BAT 启动。程序包自带 .NET 运行时，不需要额外下载或安装 .NET。

> 证书说明：安装 MSIX 时需要信任 `outputs/GanBridgeProbe-v0.24-local/GanBridgeProbe.cer`。这是随安装包提供的公钥证书；签名私钥 `.pfx` 和密码只保存在开发电脑的 `.cache/signing/`，不在仓库中，也不会提交到 GitHub。

## 常见问题

### 手机连上了，但魔方状态不动

查看 Windows 日志：

- `Count=1`：手机已完成 GAN 状态特征通知订阅；
- `已发送 GAN 转动通知 ... clients=1; Status=Success`：Windows BLE 栈已把 20 字节通知交给订阅端；
- 若仍不更新，先确认本次广播地址填入 `GAN MAC` 后再重新连接魔方星球。GAN Gen2 AES 密钥由 MAC 派生，手机和电脑 MAC 不一致时通知内容无法正确解密。

### 日志提示 `AdvertisementStatus=Aborted`

1. 先确认电脑名以 `GAN` 开头且较短（当前测试名为 `GAN356IC`），避免长 Local Name 与 128 位 GAN 服务 UUID 挤满传统广播包；
2. 关闭桥接程序并重启 Windows；
3. 如果仍失败，在 Windows 设备管理器中禁用/启用蓝牙适配器后再运行。不要删除 `.tools`、NuGet 缓存或签名证书。

旧版开发阶段的改名和蓝牙重置脚本不属于当前交付流程。

### 扫描列表中的疑似魔域设备

扫描时，名称包含 `WCU`、`MoYu`、`AI-CUBE`，或广播了魔域服务 UUID 的设备会标记为“疑似魔域”。当前开发版本会把所有疑似魔域设备统一放在列表顶部，并按首次发现顺序保持稳定；RSSI 刷新不会让它们在列表中跳来跳去，也不会改变当前选中项。多个疑似设备会一起置顶，方便选择正确的 V10 AI。

### 不要在 Windows 系统蓝牙设置中配对

手机 App 和 nRF Connect 都应从各自 App 内执行 BLE 扫描/连接。Windows 设置中的系统配对流程不是本桥接程序的测试流程。

## 连接步骤中的截图位置

为了方便实际使用，截图不单独占一个章节。你可以在下面每一步执行后，把自己的截图直接插入对应位置。仓库当前不包含真实手机截图。

### 启动 Windows 桥接程序

双击 `启动-GAN桥接程序.bat`，点击“诊断：启动 GAN 广播”。

<!-- 在这里插入截图：Windows 程序显示 GAN GATT 广播成功和广播心跳 -->

### 用 nRF Connect 读取当前 GAN 地址

手机扫描到 `GAN356IC` 后，查看它显示的当前地址。

<!-- 在这里插入截图：nRF Connect 的 GAN356IC 扫描详情、地址和服务 UUID -->

### 在桥接程序中应用 GAN MAC

把 nRF Connect 当前显示的地址填入“GAN MAC（手机看到的地址）”，点击“应用 GAN MAC”。

<!-- 在这里插入截图：GAN MAC 输入框和应用按钮 -->

### 连接魔方星球

完全退出并重新打开魔方星球，扫描并连接 `GAN356IC`。

<!-- 在这里插入截图：魔方星球连接 GAN356IC 的设备页面 -->

### 连接魔域并测试转动

电脑点击“扫描魔域魔方”，选择并连接自己的魔域 V10 AI，然后转动实体魔方。

<!-- 在这里插入截图：魔域实体魔方和魔方星球状态同步 -->

## nRF Connect 下载与使用

本项目使用 **nRF Connect for Mobile** 只做 BLE 扫描和查看广播详情，主要读取 Windows 虚拟 GAN 设备本次广播使用的地址。它不是魔方星球，也不是连接魔域实体魔方的主程序。

### 官方下载渠道

请优先使用 Nordic Semiconductor 官方渠道：

- Nordic 官方产品页：<https://www.nordicsemi.com/Products/Development-tools/nrf-connect-for-mobile>
- Android Google Play 官方页面：<https://play.google.com/store/apps/details?id=no.nordicsemi.android.mcp>
- iPhone/iPad App Store 官方页面：<https://apps.apple.com/app/nrf-connect-for-mobile/id1054362403>

如果 Google Play 或 App Store 在所在地区不可用，请从 Nordic 官方产品页查看当前可用的官方分发方式。不要从搜索结果中的不明网站下载修改版 APK，也不要使用来历不明的“蓝牙调试工具”。本项目不随仓库提供 nRF Connect APK。

### Android 首次使用

1. 从 Google Play 安装 **nRF Connect for Mobile**，开发者应显示为 Nordic Semiconductor；
2. 打开手机系统设置，给 nRF Connect 授予“附近的设备/Bluetooth”权限；Android 某些版本还可能要求位置权限，按系统提示允许即可；
3. 暂时关闭魔方星球的连接页面，避免两个 App 同时抢占同一个 BLE 连接；
4. 打开 nRF Connect，进入 **Scanner/扫描** 页面并点击 **SCAN**；
5. 在列表中找到 `GAN356IC`；本项目对应的服务 UUID 是：

   ```text
   6e400001-b5a3-f393-e0a9-e50e24dc4179
   ```

6. 点击该设备的详情/展开区域，记录本次显示的 BLE 地址；
7. 确认详情中能看到上面的 128 位服务 UUID；
8. **只读取信息，不点击 CONNECT**。如果误连接了，点击 DISCONNECT 后退出 nRF Connect；
9. 回到 Windows 桥接程序，把本次扫描显示的地址填入“GAN MAC（手机看到的地址）”，点击“应用 GAN MAC”；
10. 完全退出 nRF Connect，再重新打开魔方星球连接 `GAN356IC`。

### 如何判断扫描到的是本项目

不要只按名称判断。应该同时确认：

- 名称是 `GAN356IC`；
- 服务 UUID 是 `6e400001-b5a3-f393-e0a9-e50e24dc4179`；
- 地址是本次桥接程序启动广播后扫描得到的新地址。

每次重启电脑、重启桥接程序或重新启动广播，都建议重新扫描一次地址，因为 Windows BLE 私有广播地址可能变化。不要把电脑蓝牙适配器地址或旧日志里的地址直接当成当前 GAN MAC。

## 支持范围

当前实际测试和确认的实体魔方只有：

- 魔域 V10 AI；

其他魔域型号、其他 MoYu 型号和其他品牌目前没有实体硬件测试条件，因此不能承诺兼容。具备设备和协议分析能力的使用者可以尝试扩展，但需要自行确认：BLE 服务/特征 UUID、加密密钥派生、数据帧布局、面序和移动编码。提交其他型号的支持前，建议附上脱敏日志和独立测试结果。

## 项目结构

```text
src/GanBridgeProbe/                 C# Windows Forms、BLE 和协议转换源码
src/build-package.ps1               MSIX 构建/签名脚本
outputs/GanBridgeProbe-v0.24-local/       当前可安装包、证书和安装辅助脚本
docs/DEVELOPMENT.md                  后续开发维护记录
docs/screenshots/                    用户截图占位目录
启动-GAN桥接程序.bat                  版本检查并启动当前 MSIX
LICENSE                              GPL-3.0
THIRD_PARTY_NOTICES.md               第三方代码来源和许可证说明
```

本地构建依赖保存在 `.tools/` 与 `.cache/`，不纳入 Git。构建时显式指定未使用过的输出和 stage 目录：

```powershell
$root = (Resolve-Path '.').Path
$env:DOTNET_ROOT = Join-Path $root '.tools\dotnet'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$env:NUGET_PACKAGES = Join-Path $root '.cache\nuget'

.\src\build-package.ps1 `
  -OutputDirectory 'outputs\GanBridgeProbe-vNext' `
  -StageDirectory '.cache\stage-vNext-YYYYMMDD'
```

构建会使用本机 `.cache/signing/` 的持久签名证书。不要把 `.cache/signing/` 提交或分享。

## 参考项目与致谢

本项目感谢以下开源仓库的协议研究与实现参考：

- [csTimer](https://github.com/cs0x7f/cstimer)：GAN Gen2 mode-2/mode-4 解码规则、CubieCube 行为、MoYu/GAN 移动字段对照与计时处理思路。
- [smartcube-web-bluetooth](https://github.com/poliva/smartcube-web-bluetooth)：多品牌智能魔方 BLE 服务/特征组织、设备发现及 GAN/MoYu 协议调研。
- [cubebridge](https://github.com/bazingamc/cubebridge)：虚拟 GAN 设备身份（`GAN356iCarry` / `GAN356iC`）、GAN GATT 特征、通知布局和网关工作时序。CubieCube 状态实现及部分 GAN 帧布局参考/移植该项目，按 GPL-3.0 标注，见源文件头和 `THIRD_PARTY_NOTICES.md`。

这些仓库是研究参考，不是运行时依赖；Windows BLE GATT Server、用户界面及 MoYu→GAN 桥接流程由本项目实现。感谢原作者的分享。

## 免责声明与许可证

本项目与 GANCUBE、魔域及魔方星球没有官方关联，也未获官方认证。项目仅供个人学习、协议互操作和研究使用。第三方 App 的排位、比赛或联网功能请遵守其用户协议并自行评估账号风险。

本项目采用 GPL-3.0，详见 `LICENSE`；第三方代码来源和归属见 `THIRD_PARTY_NOTICES.md`。
