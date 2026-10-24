# Windows 安装包

使用 Inno Setup 6.4+ 编译 Windows x64 安装向导；开发机需要 .NET SDK 10。目标机不需要安装 .NET。

```bash
powershell.exe -NoProfile -File Installer/Build-Installer.ps1
```

自定义版本或编译器位置：

```bash
powershell.exe -NoProfile -File Installer/Build-Installer.ps1 -Version 1.0.1 -IsccPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

脚本依次执行 Release 构建、单元测试、安装脚本只读检查、自包含发布、依赖许可收集和 Inno 编译。任一步失败即停止。产物为 `artifacts/installer/MouseWithoutBorders-Standalone-<版本>-win-x64-Setup.exe`，附带 SHA256 文件；`payload/` 是专用暂存目录，每次打包重新生成。不会自动运行安装包。

## 用户安装流程

1. 双击 Setup.exe，接受管理员权限请求。
2. 安装到默认的 `C:\Program Files\Mouse Without Borders Standalone`。开始菜单自动创建程序与卸载入口；桌面快捷方式可选。
3. “Start Mouse Without Borders when anyone signs in to Windows” 默认选中，在所有用户的启动文件夹创建快捷方式。它表示登录后启动，不是在首次登录前启动。
4. 安装器为主程序创建一个入站 TCP 防火墙规则，只覆盖专用网络、本地子网。端口不固定，因为配置文件可修改 TCPPort；默认使用 15100/15101。Helper 和 Service 不监听网络，因此不创建规则。公共/域网络、跨子网和 VPN 需要管理员按实际网络另行配置。
5. 完成页可选择立即启动，使用发起安装的普通用户身份。服务模式仍在程序里按需开启，需要自己的管理员批准；安装器不替用户选择服务模式，也不修改配对设置。

更新时运行同 AppId 的新版安装包；目录内的主程序、Helper 与服务进程会按完整路径停止，避免占用文件。更新不会自动重启 SYSTEM 桌面实例：用户重新启动主程序后按已有 UseService 设置运行。安装器不接管其它目录注册的同名服务。

可重新运行安装包，在任务页取消登录自启。Windows 任务管理器的启动应用管理也可用于禁用该快捷方式。

## 卸载

从 Windows“已安装的应用”或开始菜单进入卸载。卸载前只移除指向当前安装目录的 `PowerToys.MWB.Service`，并停止当前目录内的三个程序；其它 PowerToys/Garage/开发副本不受清理脚本影响。失败时停止卸载，不删除文件。

安装器自动清理它创建的开始菜单、桌面、自启快捷方式，脚本移除属于本目录的专用防火墙规则。用户设置和配对数据保留在 `%LOCALAPPDATA%\Microsoft\PowerToys\MouseWithoutBorders`；不递归删除这个目录。已有 PowerToys 可能使用相同设置目录和服务名，应避免同时启用。

维护脚本日志位于执行账户的临时目录 `installation-maintenance.log`，安装器另有 Inno Setup 日志。服务注册由应用已有的 `App/Service/ServiceInstaller.cs` 实现，安装器在卸载时复用它。

## 许可与发布

`LICENSE` 保留原 MIT 许可证。`Licenses/Windows-SDK.rtf` 保留 NuGet Windows SDK projection 指向的官方许可原文（来源 `https://aka.ms/WinSDKLicenseURL`），并随安装包一起分发。`Collect-Licenses.ps1` 根据实际还原资产收集 NuGet 许可证、包元数据、.NET Runtime 原始许可证及第三方声明，放在 payload 的 `Licenses/` 与 `THIRD-PARTY-NOTICES.txt`。未知或缺少许可元数据时拒绝打包，需要人工核对。Setup 元数据标识为 standalone project，不把本派生安装包标为微软官方产品。

当前安装包没有代码签名；正式分发前需对许可证清单及签名做发布检查。

## 验证

```bash
powershell.exe -NoProfile -File Installer/Test-Installer.ps1
```

这些检查只解析脚本、执行纯路径函数并检查关键约束，不修改服务、防火墙或快捷方式。构建脚本还会检查三套进程的运行时配置为 self-contained。

实际安装验收应在 Windows 测试机或虚拟机中进行：安装并检查程序目录、开始菜单/自启快捷方式、防火墙规则；退出并重新登录验证自启；开启服务后更新；取消自启进行修复安装；卸载检查服务/规则/快捷方式移除且用户设置保留；另放一份不同目录的程序验证不会误停止或删除其服务。真实键鼠切换、配对、锁屏共享仍需两台实体 Windows 设备验证。
