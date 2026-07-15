# 轻历 QingLi

轻历是一款面向 Windows 11 x64 的本地信息万年历。单击轻历接管的任务栏时间日期区域，即可打开三栏日历，查看农历黄历、历史上的今天、节日节气倒计时，以及生日和纪念日提醒。

## 核心承诺

- 纯本地运行：月历、黄历、历史数据、生日、纪念日、备注、提醒记录和设置都保存在你的电脑上。
- 无会员、无付费墙：核心功能不需要登录、订阅或充值会员。
- 无需联网即可打开和使用月历。
- 不注入或修改 Explorer；任务栏时钟替换可随时关闭，并附带独立恢复工具。
- 面向 Windows 11 x64；打包脚本发布 `win-x64` 自包含版本。

## 数据保存位置

生日和纪念日数据库保存在：

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
powershell -ExecutionPolicy Bypass -File scripts/package.ps1 -PortableOnly
```

`scripts/package.ps1 -PortableOnly` 会先运行测试，再生成可直接解压运行的自包含 ZIP。省略 `-PortableOnly` 时，脚本还会尝试使用 Windows SDK 生成 MSIX。

详细使用方法见 [用户指南](docs/user-guide.md)，系统时钟异常时见 [恢复说明](docs/CLOCK-RECOVERY.md)。
