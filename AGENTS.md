# Mouse Without Borders 开发指引

## 范围

此仓库只开发 Windows 键鼠共享软件 Mouse Without Borders。源代码取自 Microsoft PowerToys 仓库的 `src/modules/MouseWithoutBorders/`，整理为仓库根目录下的 `App/`、`ModuleInterface/`、`MouseWithoutBorders.UnitTests/`。不要引入其他 PowerToys 功能模块。

`origin` 是 `lyqcvb/PowerToys` fork，`upstream` 是 `microsoft/PowerToys`。保留根目录 `LICENSE` 与源文件中的版权声明。若将来发布程序，先核对实际打包依赖的第三方许可。

## 代码入口

- `App/`：Windows Forms 应用、键鼠事件、网络连接、剪贴板及旧版配置界面。
- `App/Form/frmMatrix.cs` 和 `App/Form/frmMatrix.Designer.cs`：Machine Setup / Other Options / IP Mappings 界面。
- `App/Service/`：服务模式；`App/Helper/`：辅助进程。
- `ModuleInterface/`：PowerToys 模块接口，独立化时评估移除或替换。
- `MouseWithoutBorders.UnitTests/`：模块单元测试。

## 构建现状

仅整理了原始目录，**尚未改成可独立编译或运行的项目**。`App/MouseWithoutBorders.csproj` 和其他项目仍引用 PowerToys 的 `src/Common.*.props`、`src/common/`、`src/settings-ui/` 等构建文件与公共项目，目前这些依赖没有包含在工作区。`ModuleInterface/` 也依赖 PowerToys 的 C++ 公共代码和资源。未来开发独立版时，先盘点依赖、启动方式和运行时通信，再把必要的能力局部化或替换。不要通过引入其他 PowerToys 功能来满足这些依赖。

目录移动改变了 `.csproj` 内相对于仓库根目录的引用深度；在独立化之前，不能把当前项目文件当作有效的独立构建配置。构建失败不能被报告为通过。

## 开发与验证

- 遵循现有 C#、Windows Forms 和 C++ 代码风格；涉及网络协议、配对密钥、权限和服务时注意多机兼容与安全边界。
- 修改行为后更新对应测试；先构建成功，再运行单元测试。真实键鼠切换和配对行为至少用两台实体 Windows 设备验证，缺少设备时说明验证范围。
- 若向微软上游提交改动，先核对上游最新开发文档和贡献要求；本仓库整理后的路径不等于上游目录。
