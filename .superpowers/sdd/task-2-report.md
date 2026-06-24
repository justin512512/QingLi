# Task 2 Report — 生日领域模型与公历发生日期

## 结论

Task 2 已完成：已新增生日领域模型 `Birthday`、`BirthdayCalendarKind`，以及 `BirthdayOccurrenceService.GetOccurrence(Birthday birthday, int year)`；公历生日按同月同日计算，且 2 月 29 日在平年回退到 2 月 28 日。

## RED / GREEN 记录

### RED 1：公历生日测试先失败

命令：

```powershell
$env:DOTNET_CLI_HOME='E:\claude_data\tmp'; $env:DOTNET_NOLOGO='1'; $env:USERPROFILE='E:\claude_data\tmp'; $env:APPDATA='E:\claude_data\tmp\AppData\Roaming'; $env:LOCALAPPDATA='E:\claude_data\tmp\AppData\Local'; & 'E:\claude_data\.dotnet\dotnet.exe' test 'tests/QingLi.Core.Tests' --no-restore --filter Gregorian_birthday_uses_same_month_and_day
```

关键输出：

```text
error CS0246: 未能找到类型或命名空间名“Birthday”
error CS0103: 当前上下文中不存在名称“BirthdayCalendarKind”
error CS0246: 未能找到类型或命名空间名“BirthdayOccurrenceService”
```

说明：这是符合预期的“类型不存在”失败，证明测试确实在驱动实现。

### GREEN 1：最小实现后通过

命令：

```powershell
$env:DOTNET_CLI_HOME='E:\claude_data\tmp'; $env:DOTNET_NOLOGO='1'; $env:USERPROFILE='E:\claude_data\tmp'; $env:APPDATA='E:\claude_data\tmp\AppData\Roaming'; $env:LOCALAPPDATA='E:\claude_data\tmp\AppData\Local'; & 'E:\claude_data\.dotnet\dotnet.exe' test 'tests/QingLi.Core.Tests' --no-restore --filter Gregorian_birthday_uses_same_month_and_day
```

关键输出：

```text
已通过! - 失败:     0，通过:     1，已跳过:     0，总计:     1
```

### RED 2：2 月 29 日回退测试先失败

命令：

```powershell
$env:DOTNET_CLI_HOME='E:\claude_data\tmp'; $env:DOTNET_NOLOGO='1'; $env:USERPROFILE='E:\claude_data\tmp'; $env:APPDATA='E:\claude_data\tmp\AppData\Roaming'; $env:LOCALAPPDATA='E:\claude_data\tmp\AppData\Local'; & 'E:\claude_data\.dotnet\dotnet.exe' test 'tests/QingLi.Core.Tests' --no-restore --filter Gregorian_february_29_falls_back_to_last_day_in_non_leap_year
```

关键输出：

```text
System.ArgumentOutOfRangeException : Year, Month, and Day parameters describe an un-representable DateTime.
```

说明：失败点正是构造平年 2 月 29 日日期时越界，符合预期。

### GREEN 2：补回退逻辑后全套通过

命令：

```powershell
$env:DOTNET_CLI_HOME='E:\claude_data\tmp'; $env:DOTNET_NOLOGO='1'; $env:USERPROFILE='E:\claude_data\tmp'; $env:APPDATA='E:\claude_data\tmp\AppData\Roaming'; $env:LOCALAPPDATA='E:\claude_data\tmp\AppData\Local'; & 'E:\claude_data\.dotnet\dotnet.exe' test 'tests/QingLi.Core.Tests' --no-restore
```

关键输出：

```text
已通过! - 失败:     0，通过:     3，已跳过:     0，总计:     3
```

## 变更文件

- `src/QingLi.Core/Birthdays/Birthday.cs`
- `src/QingLi.Core/Birthdays/BirthdayOccurrenceService.cs`
- `tests/QingLi.Core.Tests/Birthdays/BirthdayOccurrenceServiceTests.cs`

## 实现摘要

- 新增 `BirthdayCalendarKind` 枚举，支持 `Gregorian` 与 `Lunar`。
- 新增 `Birthday` 记录，字段与任务简报一致。
- 新增 `BirthdayOccurrenceService.GetOccurrence`：
  - 非公历生日抛出 `NotSupportedException`
  - 公历生日按目标年份的同月同日返回
  - 当目标年份无该日时，使用该月最后一天回退

## 自检

- 已按 TDD 顺序执行：先写测试、验证失败、再实现、再补第二个测试、最后跑全套。
- 已确认没有修改计划文档。
- 已确认工作区内只新增了 Task 2 指定的生日相关文件。
- 已注意到本机 .NET 首次运行和 NuGet 全局配置权限问题，已通过本地 `DOTNET_CLI_HOME/APPDATA/USERPROFILE` 与显式 NuGet 配置规避，不影响最终测试结果。
