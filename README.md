# Mouse Without Borders

Windows 键鼠共享软件 Mouse Without Borders 的源码整理工作区。源代码取自 [Microsoft PowerToys](https://github.com/microsoft/PowerToys) 的 `src/modules/MouseWithoutBorders/`，当前本地将其直接放在仓库根目录，专注这一功能。

- `App/`：应用主体、Windows Forms 界面、辅助程序和服务模式。
- `ModuleInterface/`：原 PowerToys 模块接口源码。
- `MouseWithoutBorders.UnitTests/`：原模块单元测试。
- `AGENTS.md`：后续开发约定与当前依赖说明。
- `LICENSE`：源代码 MIT 许可证。

目前仅整理了目录，项目仍引用 PowerToys 未包含在此工作区的构建文件及公共组件，**尚不能直接构建或运行独立版本**。后续需要拆分和替换这些依赖，再验证构建、测试及两台 Windows 电脑间的连接。
