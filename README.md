# 轻历 QingLi

轻历是一款面向 Windows 11 x64 的本地桌面日历和生日提醒软件。当前核心版专注于月历、公历/农历生日记录、到期提醒、托盘菜单、主题设置和开机启动。

## 核心承诺

- 纯本地运行：月历、生日、备注、提醒记录和设置都保存在你的电脑上。
- 无会员、无付费墙：核心功能不需要登录、订阅或充值会员。
- 无需联网即可打开和使用月历。
- 面向 Windows 11 x64；打包脚本发布 `win-x64` 自包含版本。

## 数据保存位置

生日数据库保存在：

```text
%LOCALAPPDATA%\QingLi\qingli.db
```

通常展开为：

```text
C:\Users\<你的用户名>\AppData\Local\QingLi\qingli.db
```

应用设置也保存在 `%LOCALAPPDATA%\QingLi` 目录下。重装系统、清理用户目录或迁移电脑前，请先备份这个目录。

## 卸载和重新安装时的数据行为

- 卸载轻历不会主动删除 `%LOCALAPPDATA%\QingLi\qingli.db`。
- 再次安装后，只要数据库仍在原位置，轻历会继续使用原有生日数据。
- 如果你希望彻底清除数据，请先退出轻历，再手动删除 `%LOCALAPPDATA%\QingLi`。
- 如果未来使用 MSIX 安装包，Windows 的应用卸载机制也不应被当作数据备份；重要生日数据请自行备份 `%LOCALAPPDATA%\QingLi`。

## 从源码构建

仓库使用 .NET 8 和 WPF。推荐在 Windows 11 x64 上运行：

```powershell
dotnet test QingLi.sln -c Release
dotnet publish src/QingLi.Windows -c Release -r win-x64 --self-contained true
powershell -ExecutionPolicy Bypass -File scripts/package.ps1
```

`scripts/package.ps1` 会先运行测试，再发布自包含版本，并在 Windows SDK `makeappx.exe` 可用时生成 MSIX。若本机缺少 MSIX 打包工具，脚本会明确失败并说明如何安装，而不会假装已经生成安装包。
