# 轻历核心版测试与验收

## 自动化验证

在 Windows 11 x64 开发机上运行：

```powershell
dotnet test QingLi.sln -c Release
dotnet publish src/QingLi.Windows -c Release -r win-x64 --self-contained true
powershell -ExecutionPolicy Bypass -File scripts/package.ps1
```

期望结果：

- `dotnet test` 完成且 `0 failed`。
- `dotnet publish` 生成 `win-x64` 自包含发布输出。
- `scripts/package.ps1` 先运行测试，测试失败时立即退出。
- 打包脚本生成 `artifacts/QingLi-<version>-win-x64-portable.zip`。
- 如果 Windows SDK `makeappx.exe` 可用，打包脚本生成 `artifacts/QingLi-<version>-x64.msix`。
- 如果 `makeappx.exe` 不可用，打包脚本必须以清晰的错误消息失败，提示安装 Windows SDK MSIX Packaging Tools；不能把便携版或发布目录伪装成 MSIX 成功产物。

## 手工验收清单

在干净用户数据或备份后删除 `%LOCALAPPDATA%\QingLi` 的环境中验收：

- 全新安装或启动后，无需联网即可打开月历。
- 可以新增公历生日；退出并重启后仍存在。
- 可以新增农历生日；退出并重启后仍存在。
- 到期提醒同一天只出现一次。
- 电脑休眠跨过提醒时间后，当天唤醒会补发应提醒的生日。
- 托盘图标可见，托盘菜单可打开月历、生日管理、设置并退出应用。
- 主题切换有效，重启后仍使用保存的设置。
- 开机启动开关有效，并反映在当前用户的 Windows 启动项中。
- 卸载应用不删除 `%LOCALAPPDATA%\QingLi\qingli.db`。
- 再次安装后，如果 `%LOCALAPPDATA%\QingLi\qingli.db` 仍存在，可以继续使用原生日数据。

## 当前已知延期项

当前 WPF 核心版使用托盘气泡通知路径完成本机提醒。完整 Windows App SDK actionable toast、通知按钮、MSIX toast activation/COM activation 的端到端安装包激活链路属于后续工作；在实现前，不应把这些能力列为已完成验收项。
