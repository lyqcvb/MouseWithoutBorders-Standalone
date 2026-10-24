# 构建与贡献

欢迎改进 Mouse Without Borders (Standalone) 的代码、文档和测试。本仓库只维护 Windows 键鼠共享功能；提交改动时，请说明解决的问题、最终行为和验证范围。

[返回项目首页](README.md) · [使用指南](docs/USAGE.md) · [安装包构建](Installer/README.md)

## 开发环境

- Windows x64。
- 与 [global.json](global.json) 匹配的 .NET SDK 10：基准版本为 `10.0.400`，允许按 `latestFeature` 策略选择兼容 SDK。
- 编译安装包时另需 Inno Setup 6.4 或以上版本。

以下命令均在仓库根目录执行。

## 构建与测试

先构建解决方案：

```bash
dotnet build MouseWithoutBorders.slnx -c Debug
```

构建成功后运行单元测试：

```bash
dotnet test MouseWithoutBorders.UnitTests/MouseWithoutBorders.UnitTests.csproj -c Debug --no-build
```

输出位于 `artifacts/`。主程序构建和发布时会将 Helper 与 Service 放在同一输出目录，启动时通过相对路径调用它们。调试和分发时请保留完整输出目录。

## 发布与打包

生成 Windows x64 自包含目录：

```bash
dotnet publish App/MouseWithoutBorders.csproj -c Release -r win-x64 --self-contained true
```

生成安装包：

```bash
powershell.exe -NoProfile -File Installer/Build-Installer.ps1
```

打包脚本依次执行 Release 构建、单元测试、安装脚本检查、自包含发布、依赖许可收集和 Inno Setup 编译，任一步失败即停止。产物为 `artifacts/installer/` 下的 `Setup.exe` 与 SHA256 校验文件。版本参数、更新和卸载机制见[安装包说明](Installer/README.md)。

当前安装包未签名。对外分发前应核对实际产物中的根许可证、依赖许可和第三方声明；安装包编译成功不能替代真实安装和使用验收。

## 源码入口

| 路径 | 用途 |
| --- | --- |
| [App/](App/) | Windows Forms 主程序、键鼠事件、网络连接与剪贴板 |
| [App/Form/frmMatrix.cs](App/Form/frmMatrix.cs) | Machine Setup、Other Options、IP Mappings 设置行为 |
| [App/Helper/](App/Helper/) | 剪贴板与拖放辅助进程 |
| [App/Service/](App/Service/) | 会话 0 服务与独立注册逻辑 |
| [Shared/](Shared/) | 从 PowerToys 抽取的设置、IPC、GPO、日志及遥测桩 |
| [MouseWithoutBorders.UnitTests/](MouseWithoutBorders.UnitTests/) | 单元测试 |
| [Installer/](Installer/) | 安装向导、维护脚本与许可收集 |
| [ModuleInterface/](ModuleInterface/) | 原 PowerToys C++ 模块接口，未纳入独立构建 |

`ModuleInterface/` 仍依赖本仓库不包含的 PowerToys C++ 公共代码、CppWinRT/WIL 与 SettingsAPI。当前解决方案的构建结果不覆盖该目录。公共代码的来源与抽取差异见 [Shared/README.md](Shared/README.md)。

## 提交改动

提交 Issue 时，请提供可复现步骤、系统与程序版本，以及普通模式或服务模式等环境信息。日志和截图中请移除配对密钥及私人数据。

提交 Pull Request 时：

- 围绕一个明确问题修改，遵循现有 C#、Windows Forms 和 C++ 风格。
- 修改行为后更新对应测试；先构建成功，再运行单元测试。
- 涉及网络协议、配对密钥、IPC 权限或服务时，说明多机兼容与权限边界的影响。
- 真实键鼠切换和配对至少用两台实体 Windows 设备验证；设备不足时，明确写出尚未验证的场景。
- 报告实际构建、测试与手动验证结果，区分失败、跳过和未执行。
- 保留根 `LICENSE` 和原有版权声明，不引入其他 PowerToys 功能模块。

验证记录可参考 [docs/VALIDATION.md](docs/VALIDATION.md)。向 Microsoft PowerToys 上游提交时，请另行核对其最新贡献要求，并使用上游实际路径；本仓库的目录结构经过整理。
