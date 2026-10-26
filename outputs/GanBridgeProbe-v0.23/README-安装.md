# Windows v0.23 安装与启动

## 已在项目电脑安装

双击项目根目录 `启动-GAN桥接程序.bat`。它会检查已安装版本并确保启动 `1.0.0.23`。

## 在另一台 Windows 电脑安装

1. 解压/克隆本私有仓库；
2. 右键 PowerShell，按 README 的首次安装步骤运行 `Install-Test.ps1`；
3. 首次信任本地测试签名证书时，Windows 会弹 UAC；
4. 安装后从开始菜单启动，或双击根目录 BAT。

包为 self-contained，已包含 .NET Desktop Runtime。证书是测试签名证书，不是 Microsoft Store 证书。

## 校验

先对照 `SHA256SUMS.txt`。当前 MSIX 的 SHA-256 应为：

```text
2116CB9266D1BE7C42DF8954A3E8FE23D5BF4BAAA259EC160689DCF70AAE04A5
```
