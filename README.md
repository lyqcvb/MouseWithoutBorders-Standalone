# Mouse Without Borders (Standalone)

**一套键盘鼠标，在多台 Windows 电脑之间切换。**

Mouse Without Borders (Standalone) 从 Microsoft PowerToys 的 Mouse Without Borders 模块中整理而来，提供可独立构建、安装和运行的 Windows 键鼠共享程序。无需安装整套 PowerToys，即可在局域网中的电脑之间共享键盘、鼠标和剪贴板。

适合同时使用台式机与笔记本、需要在多台 Windows 电脑之间切换操作的场景。

[使用指南](docs/USAGE.md) · [构建与贡献](CONTRIBUTING.md) · [安装包说明](Installer/README.md) · [验证记录](docs/VALIDATION.md) · [MIT 许可证](LICENSE)

## 可以做什么

- **共享键盘和鼠标**：最多配置 4 台电脑，通过屏幕布局、Easy Mouse 或快捷键切换控制目标。
- **共享剪贴板**：在电脑之间复制、粘贴内容；当前独立版已完成双向文本剪贴板实测。
- **单独安装**：提供包含 .NET 运行时的 Windows x64 安装包，目标电脑无需另装 .NET。
- **通过界面配置**：从托盘进入设置，调整电脑布局、配对密钥、快捷键和 IP 映射。
- **按需启用服务模式**：可在设置中安装和启用服务；锁屏、登录桌面及 UAC 安全桌面等场景仍待独立版实测。

当前保留原有 Windows Forms 设置界面，主要选项为英文。代码中也保留了文件拖放等能力，其实际体验尚待验证。

## 安装与开始使用

当前面向 **Windows 10 2004（19041）及以上的 x64 系统**构建，最低系统版本来自构建和安装配置，不代表所有版本都已完成实测。至少准备两台网络互通的 Windows 电脑，建议使用相同版本的独立版。

如果已取得本项目安装包：

1. 在需要共享键鼠的每台电脑上运行 `MouseWithoutBorders-Standalone-<版本号>-win-x64-Setup.exe`，按向导完成安装。
2. 启动程序，从托盘打开设置，在 **Machine Setup** 中填写相同的 **Security Key**，添加参与共享的电脑名称并排列布局。
3. 点击 **Apply**，等待对端显示 **Connected**，再使用切换快捷键测试键盘、鼠标与文本剪贴板。

默认可用 `Ctrl+Alt+F1` 至 `Ctrl+Alt+F4` 按布局位置切换电脑；快捷键可在设置中调整。首次配置、服务模式和连接排障见[使用指南](docs/USAGE.md)。

安装包默认添加仅适用于**专用网络、本地子网**的主程序入站 TCP 规则。公共网络、域网络、跨子网或 VPN 环境需要按实际网络配置访问规则。

仓库包含安装包构建脚本；自行打包见[安装包说明](Installer/README.md)。当前安装包未进行代码签名。

## 与 PowerToys 的关系

本项目是基于 [Microsoft PowerToys](https://github.com/microsoft/PowerToys) 源码整理的独立派生项目，专注 Mouse Without Borders，不包含其他 PowerToys 功能，也不代表微软官方独立发行版。

为沿用现有进程协作方式，程序文件仍使用 `PowerToys.MouseWithoutBorders*.exe` 命名。独立版使用的服务名和用户设置路径也与 PowerToys 相同，使用前请退出其他版本的 Mouse Without Borders，避免同时运行或同时启用服务。与旧版 Microsoft Garage 程序的互通和配置迁移尚未验证。

根目录的 MIT 许可证及源文件版权声明予以保留；打包时还会收集实际依赖的第三方许可与声明。

## 当前验证情况

2026-10-24，Standalone 1.0.0 已在两台实体 Windows 电脑上完成普通桌面模式测试：两端安装成功、连接状态为 Connected、双向文本剪贴板一致，并完成快捷键切换后的键盘输入和受控鼠标移动测试。

| 验证项目 | 当前记录 |
| --- | --- |
| Release 构建 | 0 警告、0 错误 |
| 单元测试 | 189 通过、1 跳过、0 失败 |
| 安装包 | 脚本检查、Inno Setup 编译、SHA256 校验及两机安装通过 |
| 普通桌面共享 | 两机连接、文本剪贴板、键盘转发与受控鼠标移动已验证 |
| 登录自启 | 快捷方式已检查并调用；注销或重启后的完整流程待验证 |
| 服务模式 | 独立版安装、启用及 SYSTEM 桌面共享待验证 |
| 其他场景 | 屏幕边缘切换体验、文件拖放、四机协作和长期稳定性待验证 |

以上为当次测试记录，测试使用了针对对端 IP 的额外公共网络防火墙规则。具体条件和限制见[验证记录](docs/VALIDATION.md)，不代表所有系统版本和网络环境均已通过。

## 从源码构建

在 Windows 上安装与 [global.json](global.json) 匹配的 .NET SDK 10，在仓库根目录执行：

```bash
dotnet build MouseWithoutBorders.slnx -c Debug
```

构建成功后运行单元测试：

```bash
dotnet test MouseWithoutBorders.UnitTests/MouseWithoutBorders.UnitTests.csproj -c Debug --no-build
```

构建产物位于 `artifacts/`。主程序、剪贴板 Helper 和服务程序会放在同一输出目录，运行或分发时请保留完整目录。开发环境、源码入口和自包含发布命令见[构建与贡献](CONTRIBUTING.md)。

## 反馈与参与

欢迎通过本仓库的 Issues 反馈问题，或提交 Pull Request 改进代码和文档。反馈时请说明系统版本、程序版本、运行模式、网络类型、复现步骤和实际结果；日志与截图请先移除配对密钥、剪贴板内容及其他私人信息。

提交前请阅读[贡献说明](CONTRIBUTING.md)。涉及真实键鼠、服务权限或桌面切换的改动，请同时写清已测试的设备和场景，以及尚未验证的部分。

## 许可与致谢

源代码采用 [MIT License](LICENSE)。感谢 Microsoft PowerToys 及 Mouse Without Borders 的原作者和贡献者。

重新分发安装包时，请保留 `LICENSE`、随包生成的 `Licenses/` 与 `THIRD-PARTY-NOTICES.txt`，并核对实际打包依赖的许可要求。
