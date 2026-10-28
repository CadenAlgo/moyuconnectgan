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
| 当前 Windows 包 | `1.0.0.26`，MSIX 自包含 .NET 运行时 |

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

## 使用前先看：别人能否直接使用？

可以使用，但这不是 Microsoft Store 应用，第一次在一台新的 Windows 电脑上使用时，**必须同时拿到仓库中的 MSIX 安装包和 .cer 公钥证书**。证书不是私钥，也不是账号密码；它只是让 Windows 信任这个本地测试签名的安装包。

当前版本是 **v0.26-local / MSIX `1.0.0.26`**。MSIX 已经自带 .NET 运行时，普通用户不需要另外安装 .NET、Visual Studio、SDK 或编译环境。

### 普通用户需要准备什么

| 项目 | 是否需要 | 说明 |
|---|---:|---|
| Windows 10 19041 或更高版本 / Windows 11 x64 | 必须 | 当前发布包为 x64；Windows 10 建议使用较新的系统更新。 |
| 支持 BLE Central + Peripheral 的蓝牙适配器 | 必须 | 电脑既要连接实体魔域，也要向手机广播虚拟 GAN 设备。很多普通适配器只能扫描/连接，未必支持 Peripheral。 |
| 魔域 V10 AI | 必须 | 这是当前唯一实际测试过的实体魔方。其他型号暂不保证兼容。 |
| Android 手机和魔方星球（CubeStation） | 必须 | 当前完整流程在 Android 版魔方星球上验证。 |
| nRF Connect for Mobile | 建议安装 | 只用于读取虚拟 GAN 设备当前 BLE 地址和服务 UUID，不负责长期连接。 |
| 本仓库中的 `GanBridgeProbe.msix` 和 `GanBridgeProbe.cer` | 必须 | `.cer` 是公开证书；`.pfx`、密码文件和任何私钥都不需要，也不应该向使用者索取。 |
| 独立安装 .NET | 不需要 | 当前 MSIX 为 self-contained，已包含运行时。 |

> **重要：** 当前发布包使用的是个人/本地测试签名证书，不是 Microsoft Store 证书。第一次安装时 Windows 会要求信任该证书。只有在确认安装包来自你信任的仓库和发布者时，才应安装并信任它。

## 下载项目和发布包

推荐直接从 GitHub 仓库下载源码压缩包，或使用 Git 克隆：

```powershell
git clone https://github.com/CadenAlgo/moyuconnectgan.git
Set-Location .\moyuconnectgan
```

如果电脑没有安装 Git，也可以在 GitHub 页面点击 **Code → Download ZIP**，解压后进入项目目录：

- 项目仓库：[CadenAlgo/moyuconnectgan](https://github.com/CadenAlgo/moyuconnectgan)
- 当前安装目录：`outputs/GanBridgeProbe-v0.26-local/`
- MSIX：`outputs/GanBridgeProbe-v0.26-local/GanBridgeProbe.msix`
- 公钥证书：`outputs/GanBridgeProbe-v0.26-local/GanBridgeProbe.cer`

下载后请确认这两个文件都存在。可以在 PowerShell 中执行：

```powershell
Test-Path .\outputs\GanBridgeProbe-v0.26-local\GanBridgeProbe.msix
Test-Path .\outputs\GanBridgeProbe-v0.26-local\GanBridgeProbe.cer
```

两条命令都应返回 `True`。

### 校验安装包完整性

仓库同时提供 `outputs/GanBridgeProbe-v0.26-local/SHA256SUMS.txt`。当前已发布文件的 SHA-256 为：

```text
GanBridgeProbe.msix
780FADB6C39368A8599CDAC4BE92B6CD4E71181341489BD21EB301193E4A4B3B

GanBridgeProbe.cer
4AFE0B3B6161D7A7932A4DBDDFD89A3D10FFE5F7112895020CB0A80954494B56
```

在 PowerShell 中校验：

```powershell
Get-FileHash .\outputs\GanBridgeProbe-v0.26-local\GanBridgeProbe.msix -Algorithm SHA256
Get-FileHash .\outputs\GanBridgeProbe-v0.26-local\GanBridgeProbe.cer -Algorithm SHA256
Get-Content .\outputs\GanBridgeProbe-v0.26-local\SHA256SUMS.txt
```

哈希不一致时不要安装，重新下载并确认下载来源。

## 从旧版本升级（推荐方式）

普通用户**不需要先卸载旧版本，也不需要手动删除证书**。如果你是用 Git 克隆的项目，在原项目目录更新：

```powershell
git pull
```

如果之前是下载 ZIP，请从仓库页面重新下载最新 ZIP 并解压到一个新文件夹；不要把新旧版本的 `outputs` 文件夹手动混合。然后在**新版本项目目录**双击根目录的 `启动-GAN桥接程序.bat`。

当前发布版本是 `v0.26-local / 1.0.0.26`。启动脚本会关闭正在运行的旧桥接程序，并通过 MSIX 原位升级已安装应用。旧的 v0.24/v0.25 安装不需要先卸载；Windows 会保留相同应用身份和证书信任，通常也不需要重新导入 `.cer`。如果直接更新遇到证书信任错误，脚本会改用发布目录内的 `Install-Test.ps1` 处理。

如果你是通过 ZIP 下载，而不是 Git 克隆，请下载最新 ZIP 并解压到一个新目录，再双击新目录中的 `启动-GAN桥接程序.bat`。不要把新旧版本的 `outputs` 文件夹手动混合。

只有在升级过程中提示“发布者证书无效”时，才在新目录的 `outputs/GanBridgeProbe-v0.26-local/` 中运行 `Install-Test.ps1`；通常不需要运行 `Uninstall-Test.ps1`。

## 第一次安装（新 Windows 电脑）

### 方法 A：使用仓库提供的安装脚本（推荐）

打开 PowerShell，进入仓库目录中的发布包目录：

```powershell
Set-Location .\outputs\GanBridgeProbe-v0.26-local
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-Test.ps1
```

安装脚本会依次完成：

1. 使用 `GanBridgeProbe.cer` 将本地测试证书加入 Windows 的信任存储；
2. 安装 `GanBridgeProbe.msix`；
3. 启动应用。

第一次执行时，Windows 可能弹出管理员权限确认窗口；请选择允许。安装完成后，可以从开始菜单搜索 **“魔域到GAN BLE连接验证”**，也可以双击仓库根目录的 `启动-GAN桥接程序.bat`。

### 方法 B：手动信任证书后安装

如果不想运行安装脚本，可以手动安装：

1. 双击 `GanBridgeProbe.cer`；
2. 点击“安装证书”；
3. 选择“本地计算机”（可能需要管理员权限）；
4. 选择“将所有的证书放入下列存储”；
5. 将证书放入 **“受信任的人”**（Trusted People）；
6. 完成证书导入；
7. 双击 `GanBridgeProbe.msix` 安装应用。

如果双击 MSIX 时仍提示“无法安装应用”或“发布者证书无效”，请关闭安装窗口，重新运行推荐的 `Install-Test.ps1`，并在 UAC 窗口中允许证书信任操作。安装脚本会把证书导入“本地计算机\受信任的人”，因此通常需要管理员权限。

### 安装时不要做什么

- 不要下载或索要 `.pfx` 文件；
- 不要下载或索要 `password.txt`；
- 不要把仓库的 `.cache/signing/` 分享给别人；
- 不要为了运行程序安装来历不明的“破解证书”或第三方修改版；
- 不要把本项目和 Windows 系统蓝牙“配对”流程混为一谈；程序使用的是应用内 BLE 扫描/连接。

## 手机端依赖和官方下载链接

### 1. nRF Connect for Mobile（Nordic 官方）

本项目使用 nRF Connect 只做两件事：扫描 `GAN356IC`，读取当前广播地址和服务 UUID。它不是桥接程序，也不是魔方星球；读取完成后应退出，不要让它保持连接。

官方下载渠道：

- [Nordic Semiconductor 官方产品页](https://www.nordicsemi.com/Products/Development-tools/nrf-connect-for-mobile)
- [Google Play（Android）](https://play.google.com/store/apps/details?id=no.nordicsemi.android.mcp)
- [Apple App Store（iPhone/iPad）](https://apps.apple.com/app/nrf-connect-for-mobile/id1054362403)（如果你的地区页面不同，请从 Nordic 官方产品页进入）

Android 安装时请确认开发者显示为 **Nordic Semiconductor**。如果所在地区无法访问 Google Play 或 App Store，请从 Nordic 官方产品页查看当前官方分发方式；不要从不明网站下载修改版 APK。

### 2. 魔方星球 / CubeStation

魔方星球是手机端最终连接虚拟 GAN 设备并显示魔方状态的 App。请使用你所在地区能获得的官方版本，并自行确认 App 的开发者和权限。当前流程在 Android 版 **魔方星球（CubeStation）6.8** 上验证。

建议通过以下方式查找：

- [Android Google Play：CubeStation](https://play.google.com/store/apps/details?id=com.gan.cubestation)；开发者应显示为 GANCUBE/相关官方发布者；
- 在 iPhone/iPad 上打开 App Store，搜索 `CubeStation`，确认开发者和应用信息后再安装；
- 如果官方商店在所在地区没有显示，请从 [GANCUBE 官方网站](https://www.gancube.com/) 或 [CubeStation 官方网站](https://www.cubestation.com/) 查找当前下载入口。

> 不要在仓库中捆绑第三方 APK，也不要把搜索结果里的不明下载站当作官方来源。手机系统、地区和 App 版本可能变化，安装前以官方商店当前页面为准。

### 3. 手机权限

首次打开 nRF Connect 和 CubeStation 时，根据系统提示允许：

- 附近的设备 / Bluetooth；
- Android 某些版本要求的位置权限时，按系统提示允许；
- iOS 的蓝牙权限。

如果手机系统关闭了蓝牙或拒绝了附近设备权限，两个 App 都无法扫描到虚拟 GAN 设备。

## 完整连接流程

下面的顺序很重要。**先让手机连接 Windows 广播的虚拟 GAN，再让电脑连接实体魔域。** 不要让 nRF Connect、魔方星球和魔域官方 App 同时抢占同一条 BLE 链路。

### 第 0 步：检查电脑和蓝牙

1. 使用 Windows 10 19041+ 或 Windows 11 x64；
2. 打开 Windows 蓝牙设置，确认蓝牙已开启；
3. 确认蓝牙适配器支持 Peripheral/GATT Server。程序启动后顶部状态应显示 `Central=True / Peripheral=True`；如果 `Peripheral=False`，该适配器无法完成手机连接；
4. 尽量关闭其他会大量扫描或连接 BLE 的程序；
5. 将电脑名称设置为简短且以 `GAN` 开头的名称。当前测试名是 `GAN356IC`。修改电脑名称后通常需要重启 Windows；
6. 唤醒实体魔域 V10 AI，但先不要让魔域官方 App 连接它。

### 第 1 步：启动桥接程序并开始 GAN 广播

在仓库根目录双击：

```text
启动-GAN桥接程序.bat
```

如果这是第一次启动，脚本会检查安装版本；缺少 `1.0.0.26` 时会从 `outputs/GanBridgeProbe-v0.26-local/` 安装当前包。启动前请先关闭旧版桥接程序和 nRF Connect，避免旧 BLE 会话占用适配器。

程序打开后：

1. 确认窗口标题包含 `v0.26-local`；
2. 确认顶部 BLE 状态中的 `Peripheral=True`；
3. 点击 **“诊断：启动 GAN 广播”**；
4. 日志会逐次打印“GATT 广播启动尝试”；最终状态应为 `Started` 或 `StartedWithoutAllAdvertisementData`，并出现 **“GAN GATT 广播成功”**。

如果刚才广播失败过：先点击“停止广播”，等待约 3 秒，再按上述步骤重新启动。v0.26 会尝试停止并释放旧广播、等待 Windows 状态收敛后再重试；广播意外中止时还会停止本机扫描并自动恢复。

### 第 2 步：用 nRF Connect 读取当前 GAN 地址

1. 暂时关闭 CubeStation 的连接页面；
2. 打开 nRF Connect，进入 **Scanner/扫描**；
3. 点击 **SCAN**；
4. 找到名称为 `GAN356IC` 的设备；
5. 打开设备详情，确认服务 UUID 为：

   ```text
   6e400001-b5a3-f393-e0a9-e50e24dc4179
   ```

6. 记录 nRF Connect 显示的当前 BLE 地址；
7. **不要点击 CONNECT**。如果误连接，立即点击 DISCONNECT；
8. 退出 nRF Connect，避免它继续占用连接。

必须同时满足“名称 + 服务 UUID”两个条件，不要只按名字选择附近设备。

### 第 3 步：把地址填回桥接程序

1. 在桥接程序中找到 **“GAN MAC（手机看到的地址）”**；
2. 填入 nRF Connect 刚刚记录的地址，格式通常为 `AA:BB:CC:DD:EE:FF`；
3. 点击 **“应用 GAN MAC”**；
4. 等待日志出现加密 MAC 已切换的提示。

这一步不能填电脑蓝牙适配器的硬件地址，也不能使用上一次广播的旧地址。GAN Gen2 加密密钥由手机实际看到的地址派生，地址不一致时，手机可能能连接，但收不到正确的面状态和转动数据。

### 第 4 步：让 CubeStation 连接虚拟 GAN

1. 确认 nRF Connect 已退出或已断开；
2. 完全退出 CubeStation，再重新打开；
3. 在 CubeStation 的智能魔方/设备连接页面开始扫描；
4. 选择 `GAN356IC`；
5. 等待桥接程序日志出现类似：

   ```text
   远端通知订阅变化 ... Count=1
   ```

这表示手机已经订阅 GAN 状态特征。此时先不要转动实体魔方，继续下一步连接魔域。

### 第 5 步：让电脑连接实体魔域

1. 回到桥接程序，点击 **“扫描魔域魔方”**；
2. 唤醒或转动实体魔域 V10 AI，让它持续广播；
3. 在列表中选择标记为“疑似魔域”的设备；
4. 点击 **“连接选中的魔方”**；
5. 等待日志出现魔域服务连接、通知订阅成功和 A3 面状态同步；
6. 确认状态栏显示魔域已连接。

当前只有魔域 V10 AI 经过完整实机验证，其他型号可能无法解密或正确转换。

### 第 6 步：验证转动和电量

1. 先观察 CubeStation 是否显示已连接的虚拟 GAN 魔方；
2. 转动实体魔域一层；
3. 观察 CubeStation 中的虚拟魔方是否同步；
4. 如程序收到电量帧，日志会显示魔域电量，并转发给手机端；
5. 首次同步建议只做单层、90 度转动，确认方向和状态都正确后再进行连续操作。

## 日常重新连接顺序

每次重启电脑、重新启动桥接程序、停止后重新开始 GAN 广播，或者 CubeStation 重新搜索不到设备时，建议按以下顺序重新做：

1. 启动桥接程序；
2. 点击“诊断：启动 GAN 广播”；
3. 用 nRF Connect 重新扫描并读取本次新的 `GAN356IC` 地址；
4. 在桥接程序中重新应用 GAN MAC；
5. 退出 nRF Connect；
6. 重启 CubeStation 并连接 `GAN356IC`；
7. 等待 `Count=1`；
8. 再扫描并连接实体魔域。

不要长期复用旧地址，也不要把 Windows 蓝牙适配器地址当成 GAN MAC。Windows 可能为虚拟 BLE 外设使用私有地址。

## 常见问题

### 手机连上了，但魔方状态不动

查看 Windows 日志：

- `Count=1`：手机已完成 GAN 状态特征通知订阅；
- `已发送 GAN 转动通知 ... clients=1; Status=Success`：Windows BLE 栈已把 20 字节通知交给订阅端；
- 若仍不更新，先确认本次广播地址填入 `GAN MAC` 后再重新连接魔方星球。GAN Gen2 AES 密钥由 MAC 派生，手机和电脑 MAC 不一致时通知内容无法正确解密。

### 日志提示 `AdvertisementStatus=Aborted`

1. 先确认电脑名以 `GAN` 开头且较短（当前测试名为 `GAN356IC`），避免长 Local Name 与 128 位 GAN 服务 UUID 挤满传统广播包；
2. 关闭桥接程序并重启 Windows；
3. 如果仍失败，在 Windows 设备管理器中禁用/启用蓝牙适配器后再运行。v0.26 会在广播中止时停止本机扫描、释放诊断广播并自动使用退避重试；如果仍不能恢复，请重启 Windows。不要删除 `.tools`、NuGet 缓存或签名证书。

旧版开发阶段的改名和蓝牙重置脚本不属于当前交付流程。

### 扫描列表中的疑似魔域设备

扫描时，名称包含 `WCU`、`MoYu`、`AI-CUBE`，或广播了魔域服务 UUID 的设备会标记为“疑似魔域”。当前开发版本会把所有疑似魔域设备统一放在列表顶部，并按首次发现顺序保持稳定；RSSI 刷新不会让它们在列表中跳来跳去，也不会改变当前选中项。多个疑似设备会一起置顶，方便选择正确的 V10 AI。

### 不要在 Windows 系统蓝牙设置中配对

手机 App 和 nRF Connect 都应从各自 App 内执行 BLE 扫描/连接。Windows 设置中的系统配对流程不是本桥接程序的测试流程。

## 截图说明

README 当前没有捆绑真实手机截图，因为不同手机品牌、系统版本和 App 版本的界面会不同。实际截图可放入 `docs/screenshots/`，并在对应步骤附近添加图片链接。上传截图前请遮挡完整 BLE 地址、电脑名称、设备序列号和其他个人标识。

## 日志、隐私与问题反馈

### 导出日志前请脱敏

如果需要向作者反馈问题，请在桥接程序中点击“导出日志到文件”后，先用文本编辑器检查日志。请删除或打码：

- 完整 BLE 地址/MAC 地址；
- 电脑名称、设备序列号或其他个人设备标识；
- Windows 用户名、磁盘路径和用户名目录；
- 任何证书私钥、密码或 `.pfx` 内容。

可以保留错误类型、服务 UUID、通知计数和脱敏后的操作步骤，这些信息更有助于定位问题。

如果安装包或证书的 SHA-256 与 README 不一致，请同时提供下载来源和校验结果；不要把原始未脱敏日志直接公开到 issue。
## 支持范围

当前实际测试和确认的实体魔方只有：

- 魔域 V10 AI；

其他魔域型号、其他 MoYu 型号和其他品牌目前没有实体硬件测试条件，因此不能承诺兼容。具备设备和协议分析能力的使用者可以尝试扩展，但需要自行确认：BLE 服务/特征 UUID、加密密钥派生、数据帧布局、面序和移动编码。提交其他型号的支持前，建议附上脱敏日志和独立测试结果。

## 项目结构

```text
src/GanBridgeProbe/                 C# Windows Forms、BLE 和协议转换源码
src/build-package.ps1               MSIX 构建/签名脚本
outputs/GanBridgeProbe-v0.26-local/       当前可安装包、证书和安装辅助脚本
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
