# Task 4 Report

## 范围

完成 Task 4：月历 42 格生成、二十四节气本地固化表、2026 中国法定节假日 JSON 数据包、`JsonHolidayProvider`、新的 `QingLi.Infrastructure.Tests` 测试项目，以及对应测试与来源说明。未修改 brief / plan。

## RED / GREEN 证据

### 1. 月历 42 格

- RED
  - 命令：`dotnet test tests/QingLi.Core.Tests/QingLi.Core.Tests.csproj --filter Month_grid_contains_42_days_and_starts_on_configured_weekday`
  - 结果：编译失败，`QingLi.Core.Holidays` 命名空间不存在（`CS0234`），说明月历依赖的节假日模型/服务尚未实现。
- GREEN
  - 同命令复跑通过：`通过 1 / 1`

### 2. 节气命中

- RED
  - 命令：`dotnet test tests/QingLi.Core.Tests/QingLi.Core.Tests.csproj --filter Returns_known_solar_terms`
  - 结果：测试失败，`立春` 与 `夏至` 期望值均为中文节气名，实际返回 `null`。
- GREEN
  - 同命令复跑通过：`通过 2 / 2`

### 3. 2026 节假日 JSON 解析

- RED
  - 命令：`dotnet test tests/QingLi.Infrastructure.Tests/QingLi.Infrastructure.Tests.csproj --filter Reads_holiday_and_makeup_workday`
  - 结果：编译失败，`QingLi.Infrastructure.Holidays` 命名空间不存在（`CS0234`），说明 provider 与数据包尚未实现。
- GREEN
  - 同命令复跑通过：`通过 1 / 1`

### 4. 数据完整性

- 命令：`dotnet test tests/QingLi.Infrastructure.Tests/QingLi.Infrastructure.Tests.csproj --filter Package_contains_unique_2026_dates_and_all_official_ranges`
- 结果：通过 `1 / 1`
- 覆盖点：
  - `year == 2026`
  - 日期总数 `39`
  - 日期唯一
  - 官方全部放假区间完整覆盖
  - 6 个调休上班日完整覆盖：`2026-01-04`、`2026-02-14`、`2026-02-28`、`2026-05-09`、`2026-09-20`、`2026-10-10`

## 节气固化表说明

- 运行时实现：`SolarTermService` 只读取内嵌 `DayTable`
- 生成说明：`tools/SolarTerms/README.md`
- 生成方式：用本地脚本按太阳视黄经每 15° 命中一个节气，在 UTC `±2` 天窗口内二分搜索，再换算到中国时区取本地日期
- 2026 夏至核验：
  - 本地生成结果：`2026-06-21 16:20:38 +08:00`
  - 对应日期：`2026-06-21`
  - 与任务要求一致，因此未做人工硬编码修正

## 变更文件

- `QingLi.sln`
- `src/QingLi.Core/Calendars/CalendarDay.cs`
- `src/QingLi.Core/Calendars/CalendarMonthService.cs`
- `src/QingLi.Core/Calendars/SolarTermService.cs`
- `src/QingLi.Core/Holidays/HolidayDefinition.cs`
- `src/QingLi.Core/Holidays/HolidayService.cs`
- `src/QingLi.Infrastructure/Holidays/JsonHolidayProvider.cs`
- `src/QingLi.Windows/QingLi.Windows.csproj`
- `src/QingLi.Windows/Assets/Holidays/cn-2026.json`
- `tests/QingLi.Core.Tests/Calendars/CalendarMonthServiceTests.cs`
- `tests/QingLi.Core.Tests/Calendars/SolarTermServiceTests.cs`
- `tests/QingLi.Infrastructure.Tests/QingLi.Infrastructure.Tests.csproj`
- `tests/QingLi.Infrastructure.Tests/Holidays/JsonHolidayProviderTests.cs`
- `tools/SolarTerms/README.md`

## 测试

- `dotnet test tests/QingLi.Core.Tests/QingLi.Core.Tests.csproj` -> `通过 16 / 16`
- `dotnet test tests/QingLi.Infrastructure.Tests/QingLi.Infrastructure.Tests.csproj` -> `通过 2 / 2`
- `dotnet test QingLi.sln` -> `通过 18 / 18`

## 自检

- 仅改 Task 4 范围内的 Core / Infrastructure / Windows 资源 / 测试 / 工具说明 / solution
- 新建 `QingLi.Infrastructure.Tests` 并加入 solution，引用 Core 与 Infrastructure
- 2026 节假日 JSON 含 `sourceUrl`、`sourceTitle`、`publishedAt`
- 调休上班日均使用对应节日名称，且 `isWorkday = true`
- 未改动计划文件内容，未回退他人改动

## Review fix: SolarTermService year coverage and Windows asset build output

### Root cause

- `SolarTermService` 声明支持 `1901..2100`，但 `DayTable` 实际只有 `192` 行，覆盖到 `2092` 就结束；查询 `2093..2100` 会在 `date.Year - 1901` 索引时越界。
- 原有节气测试只校验了 `2026` 的两个已知日期，没有守住声明边界，也没有对整段支持区间做结构性回归。
- `cn-2026.json` 的读取测试只依赖源码路径，不能证明 Windows 项目构建产物里真的复制了该资源。

### RED

#### 1. 节气边界 / 结构性回归

- 命令：
  - `dotnet test tests/QingLi.Core.Tests/QingLi.Core.Tests.csproj --filter "Boundary_years_expose_24_solar_terms_without_throwing|Declared_supported_range_has_200_years_and_each_year_returns_24_terms|Returns_known_solar_terms"`
- 首次结果：
  - 编译失败：`SolarTermService` 不包含 `MinSupportedYear` / `MaxSupportedYear`（`CS0117`）

#### 2. Windows 构建输出中的节假日数据文件

- 命令：
  - `dotnet test tests/QingLi.Infrastructure.Tests/QingLi.Infrastructure.Tests.csproj --filter Windows_build_output_contains_holiday_asset_readable_by_provider`
- 首次结果：
  - 失败：测试内直接启动 `dotnet` 进程时找不到可执行文件

### GREEN

#### 1. 节气覆盖修复

- 变更：
  - 新增 `SolarTermService.MinSupportedYear` / `MaxSupportedYear`
  - 将 `TermNames` 修正为正常 UTF-8 中文
  - 用同一生成规则补齐 `2093..2100` 的 8 个年度条目
  - `DayTable` 总数从 `192` 补到 `200`
- 复跑命令：
  - `dotnet test tests/QingLi.Core.Tests/QingLi.Core.Tests.csproj --filter "Boundary_years_expose_24_solar_terms_without_throwing|Declared_supported_range_has_200_years_and_each_year_returns_24_terms|Returns_known_solar_terms"`
- 结果：
  - `通过 7 / 7`

#### 2. Windows 输出资源复制验证

- 变更：
  - 在 `tests/QingLi.Infrastructure.Tests/QingLi.Infrastructure.Tests.csproj` 新增 `BuildQingLiWindowsForArtifactChecks`，于 `VSTest` 前构建 `src/QingLi.Windows/QingLi.Windows.csproj`
  - 新增测试 `Windows_build_output_contains_holiday_asset_readable_by_provider`
  - 测试读取 `src/QingLi.Windows/bin/Debug/net8.0-windows/Assets/Holidays/cn-2026.json`，不再把源码相对路径作为唯一证据
- 复跑命令：
  - `dotnet test tests/QingLi.Infrastructure.Tests/QingLi.Infrastructure.Tests.csproj --filter Windows_build_output_contains_holiday_asset_readable_by_provider`
- 结果：
  - `通过 1 / 1`

### Final verification after review fix

- 聚焦测试
  - `dotnet test tests/QingLi.Core.Tests/QingLi.Core.Tests.csproj --filter "Boundary_years_expose_24_solar_terms_without_throwing|Declared_supported_range_has_200_years_and_each_year_returns_24_terms|Returns_known_solar_terms"`
    - `通过 7 / 7`
  - `dotnet test tests/QingLi.Infrastructure.Tests/QingLi.Infrastructure.Tests.csproj --filter Windows_build_output_contains_holiday_asset_readable_by_provider`
    - `通过 1 / 1`
- 分项目测试
  - `dotnet test tests/QingLi.Core.Tests/QingLi.Core.Tests.csproj`
    - `通过 21 / 21`
  - `dotnet test tests/QingLi.Infrastructure.Tests/QingLi.Infrastructure.Tests.csproj`
    - `通过 3 / 3`
- Solution 测试
  - `dotnet test QingLi.sln`
    - `通过 24 / 24`
