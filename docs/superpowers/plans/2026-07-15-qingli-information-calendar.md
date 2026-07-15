# QingLi Information Calendar Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 将轻历升级为离线优先的三栏信息万年历，通过任务栏时间日期区域打开，提供黄历、历史上的今天、节日节气倒计时、生日和纪念日提醒。

**Architecture:** 保留现有 Core / Infrastructure / Windows 三层和安全任务栏替换机制。Core 定义领域模型与聚合服务，Infrastructure 负责 SQLite 和离线 JSON，Windows 负责 WPF 视图、通知与应用组合；所有外部数据更新都写入候选文件，校验成功后原子替换，失败时继续使用内置数据。

**Tech Stack:** .NET 8, WPF, xUnit, Microsoft.Data.Sqlite 8.0.22, lunar-csharp 1.6.8 (MIT), System.Text.Json

---

## Task 1: 接入离线黄历引擎

**Files:**
- Modify: `src/QingLi.Core/QingLi.Core.csproj`
- Create: `src/QingLi.Core/Almanac/AlmanacDay.cs`
- Create: `src/QingLi.Core/Almanac/IAlmanacService.cs`
- Create: `src/QingLi.Core/Almanac/LunarSharpAlmanacService.cs`
- Create: `tests/QingLi.Core.Tests/Almanac/LunarSharpAlmanacServiceTests.cs`

**Step 1: 写固定日期失败测试**

测试 `2026-07-15` 返回农历六月初二、丙午年、生肖马、乙未月、庚寅日，并验证宜包含“开市”、忌包含“入宅”。另测服务不依赖网络且返回的宜忌集合不为 null。

**Step 2: 运行测试并确认失败**

Run: `E:\claude_data\.dotnet\dotnet.exe test tests/QingLi.Core.Tests/QingLi.Core.Tests.csproj --filter LunarSharpAlmanacServiceTests`

Expected: 编译失败，因为 `IAlmanacService` 和实现尚不存在。

**Step 3: 最小实现**

在项目文件中固定 `lunar-csharp` 为 `1.6.8`。定义不可变 `AlmanacDay`，字段为日期、农历月日、年/月/日干支、生肖、节气、节日、宜、忌。适配器使用 `new Solar(year, month, day, 0, 0, 0).Lunar` 映射属性，并复制集合，避免 UI 依赖第三方类型。

**Step 4: 运行测试并确认通过**

Run: `E:\claude_data\.dotnet\dotnet.exe test tests/QingLi.Core.Tests/QingLi.Core.Tests.csproj --filter LunarSharpAlmanacServiceTests`

Expected: PASS。

**Step 5: 提交**

```text
git add src/QingLi.Core tests/QingLi.Core.Tests
git commit -m "feat: add offline almanac service"
```

## Task 2: 建立“历史上的今天”离线数据包

**Files:**
- Create: `src/QingLi.Core/History/HistoryTodayEntry.cs`
- Create: `src/QingLi.Core/History/IHistoryTodayProvider.cs`
- Create: `src/QingLi.Infrastructure/History/JsonHistoryTodayProvider.cs`
- Create: `src/QingLi.Windows/Assets/History/history-today.zh-CN.json`
- Modify: `src/QingLi.Windows/QingLi.Windows.csproj`
- Create: `scripts/build-history-today.ps1`
- Create: `tests/QingLi.Infrastructure.Tests/History/JsonHistoryTodayProviderTests.cs`

**Step 1: 写包完整性失败测试**

测试 JSON 必须包含 366 个 `MM-dd` 键（含 `02-29`），每个键最多 10 条；每条包含年份、中文摘要、来源名称和 HTTPS 来源链接；查询 `07-15` 按年份排序并返回只读结果。

**Step 2: 运行测试并确认失败**

Run: `E:\claude_data\.dotnet\dotnet.exe test tests/QingLi.Infrastructure.Tests/QingLi.Infrastructure.Tests.csproj --filter JsonHistoryTodayProviderTests`

Expected: 编译失败，因为 provider 和数据包尚不存在。

**Step 3: 实现数据契约与严格读取器**

读取器启动时一次性反序列化并校验键格式、条数、必填字段和 URL；任何一条非法都拒绝整个外部包。查询接口只按月日工作，闰日数据不会丢失。

**Step 4: 编写发布期生成脚本并生成快照**

脚本逐日调用 Wikimedia On This Day 中文源，标准化为 366 键，最多保留 10 条，并写入源名称、原文链接、生成时间和许可说明。运行期不得调用该脚本或访问网络。

Run: `powershell -ExecutionPolicy Bypass -File scripts/build-history-today.ps1 -OutputPath src/QingLi.Windows/Assets/History/history-today.zh-CN.json`

Expected: 生成完整且可重复校验的 UTF-8 JSON。

**Step 5: 复制数据到输出目录并跑测试**

在 Windows 项目中将文件标记为 `Content` 和 `CopyToOutputDirectory=PreserveNewest`。

Run: `E:\claude_data\.dotnet\dotnet.exe test tests/QingLi.Infrastructure.Tests/QingLi.Infrastructure.Tests.csproj --filter JsonHistoryTodayProviderTests`

Expected: PASS。

**Step 6: 提交**

```text
git add src/QingLi.Core/History src/QingLi.Infrastructure/History src/QingLi.Windows/Assets/History src/QingLi.Windows/QingLi.Windows.csproj scripts tests/QingLi.Infrastructure.Tests/History
git commit -m "feat: add offline history today data"
```

## Task 3: 增加纪念日领域模型和本地存储

**Files:**
- Create: `src/QingLi.Core/Anniversaries/Anniversary.cs`
- Create: `src/QingLi.Core/Anniversaries/IAnniversaryRepository.cs`
- Create: `src/QingLi.Core/Anniversaries/AnniversaryOccurrenceService.cs`
- Modify: `src/QingLi.Infrastructure/Data/DatabaseMigrator.cs`
- Create: `src/QingLi.Infrastructure/Anniversaries/SqliteAnniversaryRepository.cs`
- Create: `tests/QingLi.Core.Tests/Anniversaries/AnniversaryOccurrenceServiceTests.cs`
- Create: `tests/QingLi.Infrastructure.Tests/Anniversaries/SqliteAnniversaryRepositoryTests.cs`

**Step 1: 写领域失败测试**

覆盖公历、农历、农历闰月、跨年下一次发生日期、2 月 29 日策略、禁用项目。模型字段与生日保持一致，并增加标题、开始年份和备注。

**Step 2: 写仓储失败测试**

在临时数据库迁移后验证保存、覆盖、按 ID 查询、删除、重新打开仍存在；同时验证旧数据库只有 birthdays/settings/reminder_history 时可无损升级。

**Step 3: 运行测试并确认失败**

Run: `E:\claude_data\.dotnet\dotnet.exe test tests/QingLi.Core.Tests/QingLi.Core.Tests.csproj --filter Anniversary`

Run: `E:\claude_data\.dotnet\dotnet.exe test tests/QingLi.Infrastructure.Tests/QingLi.Infrastructure.Tests.csproj --filter Anniversary`

Expected: 编译失败，因为模型、迁移和仓储尚不存在。

**Step 4: 实现模型、发生日期与迁移**

复用现有公历/农历换算规则；新增 `anniversaries` 表和索引。迁移必须幂等，不能删除或改写 birthdays 数据。

**Step 5: 实现 SQLite CRUD 并跑测试**

Run: `E:\claude_data\.dotnet\dotnet.exe test tests/QingLi.Core.Tests/QingLi.Core.Tests.csproj --filter Anniversary`

Run: `E:\claude_data\.dotnet\dotnet.exe test tests/QingLi.Infrastructure.Tests/QingLi.Infrastructure.Tests.csproj --filter Anniversary`

Expected: PASS。

**Step 6: 提交**

```text
git add src/QingLi.Core/Anniversaries src/QingLi.Infrastructure/Data src/QingLi.Infrastructure/Anniversaries tests
git commit -m "feat: add local anniversary tracking"
```

## Task 4: 将提醒管线扩展为生日与纪念日

**Files:**
- Modify: `src/QingLi.Core/Reminders/ReminderCandidate.cs`
- Modify: `src/QingLi.Core/Reminders/ReminderPlanner.cs`
- Modify: `src/QingLi.Core/Reminders/IReminderHistoryRepository.cs`
- Modify: `src/QingLi.Infrastructure/Data/DatabaseMigrator.cs`
- Modify: `src/QingLi.Infrastructure/Reminders/SqliteReminderHistoryRepository.cs`
- Modify: `src/QingLi.Windows/Services/ReminderScheduler.cs`
- Modify: `src/QingLi.Windows/Services/NotificationPayloadBuilder.cs`
- Modify: `src/QingLi.Windows/Services/IReminderNotificationSink.cs`
- Modify: `tests/QingLi.Core.Tests/Reminders/ReminderPlannerTests.cs`
- Modify: `tests/QingLi.Infrastructure.Tests/Reminders/SqliteReminderHistoryRepositoryTests.cs`
- Modify: `tests/QingLi.Windows.Tests/Services/ReminderSchedulerTests.cs`

**Step 1: 写兼容性失败测试**

给提醒候选增加 `ReminderSubjectKind` 与 `SubjectId`。测试生日原行为不变，纪念日能按提前天数和本地时间排程；同一对象同一发生日期只通知一次；重启不重复通知；禁用项不排程。

**Step 2: 写数据库升级失败测试**

用旧版 schema 创建历史记录后执行迁移，验证旧生日记录仍能防重复，新纪念日记录可写入，唯一键由 `(subject_kind, subject_id, occurrence_date)` 表达。

**Step 3: 运行相关测试并确认失败**

Run: `E:\claude_data\.dotnet\dotnet.exe test tests/QingLi.Core.Tests/QingLi.Core.Tests.csproj --filter Reminder`

Run: `E:\claude_data\.dotnet\dotnet.exe test tests/QingLi.Infrastructure.Tests/QingLi.Infrastructure.Tests.csproj --filter ReminderHistory`

Run: `E:\claude_data\.dotnet\dotnet.exe test tests/QingLi.Windows.Tests/QingLi.Windows.Tests.csproj --filter ReminderScheduler`

Expected: 新断言失败或编译失败。

**Step 4: 实现无损迁移和通用排程**

通过新表复制/重命名的事务迁移保留旧 birthday_id，并将其映射为 `Birthday`。调度器同时读取两个仓储，通知标题明确显示“生日提醒”或“纪念日提醒”。

**Step 5: 运行相关测试并确认通过**

重复 Step 3 三条命令。

Expected: PASS。

**Step 6: 提交**

```text
git add src/QingLi.Core/Reminders src/QingLi.Infrastructure src/QingLi.Windows/Services tests
git commit -m "feat: support anniversary reminders"
```

## Task 5: 聚合未来 90 天的重要日期

**Files:**
- Create: `src/QingLi.Core/Upcoming/UpcomingEventKind.cs`
- Create: `src/QingLi.Core/Upcoming/UpcomingEvent.cs`
- Create: `src/QingLi.Core/Upcoming/IUpcomingEventService.cs`
- Create: `src/QingLi.Core/Upcoming/UpcomingEventService.cs`
- Create: `tests/QingLi.Core.Tests/Upcoming/UpcomingEventServiceTests.cs`

**Step 1: 写排序和去重失败测试**

固定今天为 `2026-07-15`，聚合未来 90 天的节气、法定节假日、生日和纪念日。按天数升序，同日按法定假日、节气、生日、纪念日排序；结果包含日期、剩余天数、类别、标题、休/班标记和对象 ID。过去事件排除，今天显示“今天”，同一来源不重复。

**Step 2: 运行测试并确认失败**

Run: `E:\claude_data\.dotnet\dotnet.exe test tests/QingLi.Core.Tests/QingLi.Core.Tests.csproj --filter UpcomingEventServiceTests`

Expected: 编译失败，因为聚合服务尚不存在。

**Step 3: 最小实现**

服务只依赖既有日期服务、假日 provider、两个仓储和 `TimeProvider`/传入日期；不引用 WPF。结果为不可变列表，跨年时同时查询相邻年份假日包。

**Step 4: 运行测试并确认通过**

Run: `E:\claude_data\.dotnet\dotnet.exe test tests/QingLi.Core.Tests/QingLi.Core.Tests.csproj --filter UpcomingEventServiceTests`

Expected: PASS。

**Step 5: 提交**

```text
git add src/QingLi.Core/Upcoming tests/QingLi.Core.Tests/Upcoming
git commit -m "feat: aggregate upcoming calendar events"
```

## Task 6: 建立三栏仪表盘 ViewModel

**Files:**
- Create: `src/QingLi.Windows/ViewModels/CalendarDashboardViewModel.cs`
- Create: `src/QingLi.Windows/ViewModels/HistoryTodayItemViewModel.cs`
- Create: `src/QingLi.Windows/ViewModels/UpcomingEventViewModel.cs`
- Create: `src/QingLi.Windows/ViewModels/AlmanacSummaryViewModel.cs`
- Modify: `src/QingLi.Windows/ViewModels/CalendarDayViewModel.cs`
- Modify: `src/QingLi.Windows/ViewModels/CalendarPopupViewModel.cs`
- Create: `tests/QingLi.Windows.Tests/ViewModels/CalendarDashboardViewModelTests.cs`
- Modify: `tests/QingLi.Windows.Tests/ViewModels/CalendarPopupViewModelTests.cs`

**Step 1: 写状态同步失败测试**

验证初始化选中今天并同时填充三栏；点击月份日期会更新大号日期、黄历、历史和倒计时；上月/下月/今天命令生成正确 42 格；连续快速切月时较旧异步结果不能覆盖新月份；加载失败时保留日历并显示局部错误状态。

**Step 2: 运行测试并确认失败**

Run: `E:\claude_data\.dotnet\dotnet.exe test tests/QingLi.Windows.Tests/QingLi.Windows.Tests.csproj --filter "CalendarDashboardViewModel|CalendarPopupViewModel"`

Expected: 编译失败或新断言失败。

**Step 3: 实现组合 ViewModel**

`CalendarDashboardViewModel` 作为弹窗唯一页面状态入口，包装现有月份 VM；使用 `CancellationTokenSource` 和请求序号防止陈旧结果写回。黄历按日期缓存，历史按 `MM-dd` 缓存，未来事件在日期/仓储变化时刷新。

**Step 4: 运行测试并确认通过**

重复 Step 2 命令。

Expected: PASS。

**Step 5: 提交**

```text
git add src/QingLi.Windows/ViewModels tests/QingLi.Windows.Tests/ViewModels
git commit -m "feat: add information calendar dashboard state"
```

## Task 7: 增加纪念日编辑和快捷操作

**Files:**
- Create: `src/QingLi.Windows/ViewModels/AnniversaryEditorViewModel.cs`
- Create: `src/QingLi.Windows/Views/AnniversaryEditorWindow.xaml`
- Create: `src/QingLi.Windows/Views/AnniversaryEditorWindow.xaml.cs`
- Modify: `src/QingLi.Windows/Views/BirthdayEditorWindow.xaml`
- Modify: `src/QingLi.Windows/ViewModels/BirthdayEditorViewModel.cs`
- Create: `tests/QingLi.Windows.Tests/ViewModels/AnniversaryEditorViewModelTests.cs`
- Modify: `tests/QingLi.Windows.Tests/ViewModels/BirthdayEditorViewModelTests.cs`

**Step 1: 写编辑验证失败测试**

覆盖标题必填、日期范围、农历闰月、提前提醒天数、提醒时间、保存/取消、编辑已有记录。生日编辑器也应暴露统一的保存完成事件，以便仪表盘即时刷新。

**Step 2: 运行测试并确认失败**

Run: `E:\claude_data\.dotnet\dotnet.exe test tests/QingLi.Windows.Tests/QingLi.Windows.Tests.csproj --filter "AnniversaryEditor|BirthdayEditor"`

Expected: 编译失败或新断言失败。

**Step 3: 实现编辑窗口**

沿用生日编辑器的控件和验证风格；快捷新增默认使用当前选中日期。保存成功后关闭窗口并触发仪表盘和提醒调度器刷新。

**Step 4: 运行测试并确认通过**

重复 Step 2 命令。

Expected: PASS。

**Step 5: 提交**

```text
git add src/QingLi.Windows/ViewModels src/QingLi.Windows/Views tests/QingLi.Windows.Tests/ViewModels
git commit -m "feat: add anniversary editor and quick actions"
```

## Task 8: 将弹窗改造成 900×440 三栏信息界面

**Files:**
- Modify: `src/QingLi.Windows/Views/CalendarPopupWindow.xaml`
- Modify: `src/QingLi.Windows/Views/CalendarPopupWindow.xaml.cs`
- Create: `src/QingLi.Windows/Views/Converters/UpcomingEventKindToBrushConverter.cs`
- Create: `src/QingLi.Windows/Views/Converters/BooleanToWorkRestBadgeConverter.cs`
- Modify: `src/QingLi.Windows/App.xaml`
- Create: `tests/QingLi.Windows.Tests/Views/CalendarPopupLayoutTests.cs`

**Step 1: 写结构失败测试**

解析 XAML 并断言：窗口默认 900×440、三列宽度约 260/1*/260；左栏有历史标题与独立滚动区；中栏有大日期、农历干支生肖、宜忌和 42 格日历；右栏有最近节日节气与快捷新增按钮；左右列表滚动不移动中间日历；窗口不出现在任务栏和 Alt+Tab。

**Step 2: 运行测试并确认失败**

Run: `E:\claude_data\.dotnet\dotnet.exe test tests/QingLi.Windows.Tests/QingLi.Windows.Tests.csproj --filter CalendarPopupLayoutTests`

Expected: 新结构断言失败。

**Step 3: 实现紧凑三栏 UI**

按已批准视觉稿实现：浅色卡片、圆角、蓝色选中态、周末红色、非本月灰色、休/班角标。历史年份使用蓝色胶囊，倒计时使用右对齐胶囊；宜忌各自限高并可滚动，避免撑高窗口。

**Step 4: 接入交互**

日期格绑定选择命令；历史条目打开其 HTTPS 来源；右栏生日/纪念日条目打开编辑；“新增生日”“新增纪念日”“设置”连接对应窗口。键盘支持 Esc 关闭、左右键切日、PageUp/PageDown 切月、Home 回今天。

**Step 5: 运行布局和 ViewModel 测试**

Run: `E:\claude_data\.dotnet\dotnet.exe test tests/QingLi.Windows.Tests/QingLi.Windows.Tests.csproj --filter "CalendarPopupLayout|CalendarDashboard"`

Expected: PASS。

**Step 6: 提交**

```text
git add src/QingLi.Windows/Views src/QingLi.Windows/App.xaml tests/QingLi.Windows.Tests/Views
git commit -m "feat: redesign calendar popup as three-column dashboard"
```

## Task 9: 完成应用组合、任务栏打开和多显示器定位

**Files:**
- Modify: `src/QingLi.Windows/App.xaml.cs`
- Modify: `src/QingLi.Windows/Taskbar/TaskbarClockHost.cs`
- Modify: `src/QingLi.Windows/Views/CalendarPopupWindow.xaml.cs`
- Modify: `tests/QingLi.Windows.Tests/Taskbar/TaskbarClockHostTests.cs`
- Create: `tests/QingLi.Windows.Tests/Views/CalendarPopupPlacementTests.cs`
- Modify: `tests/QingLi.Windows.Tests/AppCompositionTests.cs`

**Step 1: 写组合与定位失败测试**

验证应用创建黄历、历史、纪念日和未来事件服务；单击替换后的整个时间日期区域只切换一个弹窗；弹窗贴近被点击任务栏时钟，保持在当前显示器工作区内；任务栏在上/下/左/右及不同 DPI 时均不越界；失焦宽限期仍为 750ms。

**Step 2: 运行测试并确认失败**

Run: `E:\claude_data\.dotnet\dotnet.exe test tests/QingLi.Windows.Tests/QingLi.Windows.Tests.csproj --filter "TaskbarClockHost|CalendarPopupPlacement|AppComposition"`

Expected: 新组合或定位断言失败。

**Step 3: 实现依赖组合与刷新生命周期**

在 `App.xaml.cs` 只创建单例仓储/provider，窗口每次打开刷新“今天”和到期倒计时；关闭采用隐藏复用。任务栏替换仍使用现有安全覆盖层，不注入 Explorer，不改写系统组件；异常退出恢复系统时钟的既有保护必须保留。

**Step 4: 实现工作区约束定位**

从点击的任务栏时钟矩形和对应显示器工作区计算窗口位置，再按 DPI 转成 WPF 单位；优先向任务栏内侧展开，空间不足时夹紧。

**Step 5: 运行测试并确认通过**

重复 Step 2 命令。

Expected: PASS。

**Step 6: 提交**

```text
git add src/QingLi.Windows tests/QingLi.Windows.Tests
git commit -m "feat: wire dashboard to taskbar clock"
```

## Task 10: 离线包更新、回退与完整性保护

**Files:**
- Create: `src/QingLi.Infrastructure/Updates/DataPackageManifest.cs`
- Create: `src/QingLi.Infrastructure/Updates/ValidatedDataPackageStore.cs`
- Create: `src/QingLi.Infrastructure/Updates/IDataPackageDownloader.cs`
- Create: `src/QingLi.Windows/Services/HttpDataPackageDownloader.cs`
- Modify: `src/QingLi.Windows/App.xaml.cs`
- Create: `tests/QingLi.Infrastructure.Tests/Updates/ValidatedDataPackageStoreTests.cs`
- Create: `tests/QingLi.Windows.Tests/Services/HttpDataPackageDownloaderTests.cs`

**Step 1: 写回退失败测试**

覆盖：无网络使用内置包；下载超时不影响启动；SHA-256 不匹配拒绝；JSON schema 非法拒绝；版本较旧拒绝；合法包先写临时文件再原子替换；替换中断后下次仍可读取上一版本。更新默认关闭或仅手动触发，不发送生日/纪念日数据。

**Step 2: 运行测试并确认失败**

Run: `E:\claude_data\.dotnet\dotnet.exe test tests/QingLi.Infrastructure.Tests/QingLi.Infrastructure.Tests.csproj --filter ValidatedDataPackageStoreTests`

Run: `E:\claude_data\.dotnet\dotnet.exe test tests/QingLi.Windows.Tests/QingLi.Windows.Tests.csproj --filter HttpDataPackageDownloaderTests`

Expected: 编译失败，因为更新组件尚不存在。

**Step 3: 实现校验存储与下载边界**

Infrastructure 只处理 manifest、散列、版本和原子文件操作；Windows 下载器使用短超时和 HTTPS。历史与假日 provider 的读取顺序为“已验证本地更新包 → 随程序内置包”，任何异常自动降级。

**Step 4: 运行测试并确认通过**

重复 Step 2 两条命令。

Expected: PASS。

**Step 5: 提交**

```text
git add src/QingLi.Infrastructure/Updates src/QingLi.Windows/Services src/QingLi.Windows/App.xaml.cs tests
git commit -m "feat: add verified offline data updates"
```

## Task 11: 全量回归、性能验收与发布包

**Files:**
- Modify: `README.md`
- Modify: `docs/user-guide.md`
- Modify: `scripts/package-portable.ps1`
- Create: `docs/test-matrix-information-calendar.md`

**Step 1: 扩充用户说明**

说明任务栏时间日期区域由轻历的安全覆盖层接管，而不是替换 Windows 内部日历；记录启用/停用、恢复系统时钟、生日/纪念日、本地数据位置、离线可用性和手动更新方法。

**Step 2: 运行全量自动测试**

Run: `E:\claude_data\.dotnet\dotnet.exe test QingLi.sln --configuration Release`

Expected: 全部 PASS，且测试数不少于改造前的 161。

**Step 3: 做性能验收**

在 Release 构建中测量：首次打开弹窗小于 500ms；同进程再次打开小于 150ms；切月小于 150ms；空闲时无持续网络请求；关闭弹窗后无高频计时器。将机器配置、测量方法和结果写入测试矩阵。

**Step 4: 做 Windows 人工矩阵**

逐项验证 Windows 11 100%/125%/150% DPI、主/副显示器、任务栏底/顶/左/右、Explorer 重启、应用崩溃恢复、首次启动、开机启动、Esc/失焦关闭、通知点击、无网启动、损坏更新包回退。每项记录通过/失败和截图路径。

**Step 5: 生成便携 EXE 包并做干净目录冒烟测试**

Run: `powershell -ExecutionPolicy Bypass -File scripts/package-portable.ps1 -Configuration Release -Runtime win-x64`

Expected: `artifacts` 中产生包含 EXE、依赖 DLL、假日包和历史包的 ZIP；解压到新目录后可启动、可点击任务栏时钟打开三栏日历、可新增生日和纪念日、退出后恢复系统时钟。

**Step 6: 记录散列并检查工作区**

Run: `Get-FileHash artifacts\QingLi-*-win-x64-portable.zip -Algorithm SHA256`

Run: `git diff --check`

Run: `git status --short`

Expected: 有 SHA-256；无空白错误；只有本计划范围内的预期改动或用户原有未跟踪文件。

**Step 7: 提交**

```text
git add README.md docs scripts/package-portable.ps1
git commit -m "docs: prepare information calendar release"
```

## 实施完成前的规格覆盖检查

- 任务栏时间日期整个区域可点击，且保留安全恢复路径。
- 弹窗为约 900×440 的三栏布局，不是系统日历的简单复刻。
- 左栏含完整来源信息的“历史上的今天”。
- 中栏含农历、干支、生肖、节气、节日、宜忌、休班标记和 42 格月历。
- 右栏含未来节日、节气、假日、生日、纪念日及倒计时。
- 支持生日、纪念日新增编辑和系统通知，旧生日数据无损升级。
- 无账号、会员、支付、天气或运行期强制联网。
- 更新包必须 HTTPS、版本校验、SHA-256 校验和失败回退。
- 全量自动测试、Windows 人工矩阵、性能数据和最终 ZIP 散列均已记录。

## 计划自检命令

Run: `rg -n "T[B]D|T[O]DO|implement la[t]er|same a[s]|write tes[t]s|适[当]|类[似]" docs/superpowers/plans/2026-07-15-qingli-information-calendar.md`

Expected: 无结果。

Run: `git diff --check -- docs/superpowers/plans/2026-07-15-qingli-information-calendar.md`

Expected: 无输出。
