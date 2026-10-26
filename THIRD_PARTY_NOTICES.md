# Third-party notices

本项目并非所有代码都原创。协议常量、字段布局等属于协议研究资料；部分魔方状态及 GAN Gen2 数据结构实现参考/移植自下列开源项目。相关源码按其许可证要求随本项目提供。

## cubebridge

- 项目：<https://github.com/bazingamc/cubebridge>
- 许可证：GPL-3.0（完整文本见根目录 `LICENSE`）
- 本项目参考/移植范围：`CubeState`/CubieCube 状态表示与转动、GAN Gen2 mode-2/mode-4 位布局和身份字段；具体源文件头部及自检文件包含来源注释。
- 仅作协议/结构参考而未直接复制其 ESP-IDF GATT、任务队列与 NVS 代码：Windows GATT 服务、广告与用户界面为本项目实现。

## smartcube-web-bluetooth

- 项目：<https://github.com/poliva/smartcube-web-bluetooth>
- 许可证：MIT
- 参考范围：多品牌智能魔方 BLE 服务/特征发现、GAN 与 MoYu 协议研究思路；未将其作为运行时依赖。

## csTimer

- 项目：<https://github.com/cs0x7f/cstimer>
- 许可证：GPL-3.0
- 参考范围：GAN Gen2 数据解析/位布局、CubieCube 行为和 MoYu 数据字段验证。

## gan-web-bluetooth

- 项目：<https://github.com/afedotov/gan-web-bluetooth>
- 许可证：MIT
- 参考范围：GAN BLE 协议研究资料。

本项目整体以 GPL-3.0 发布。各来源代码、数据和项目仍归其作者所有。本项目与 GANCUBE、魔域或魔方星球没有官方关联。
