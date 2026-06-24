# 轻历核心版 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建一款可安装的 Windows 11 本地桌面应用，提供托盘入口、万年历、公历/农历生日管理和可靠提醒。

**Architecture:** 使用 WPF 承载无边框弹窗和管理界面，业务逻辑放入不依赖 UI 的 Core 项目，SQLite 存储放入 Infrastructure 项目，Windows 托盘与通知放入 Windows 项目。所有日期、提醒和存储行为先通过 xUnit 测试定义，再连接到界面。

**Tech Stack:** C# 12、.NET 8、WPF、xUnit、Microsoft.Data.Sqlite、Windows App SDK App Notifications、MSIX。

## Global Constraints

- 目标系统仅为 Windows 11 x64。
- 应用必须以普通用户权限运行；不得要求管理员权限。
- 用户个人数据仅保存在本机，核心功能不依赖网络。
- 不包含账号、云同步、广告、会员、天气和黄历宜忌。
- 生产代码必须遵循测试先行：先观察测试因缺少行为而失败，再写最小实现。
- 数据库路径固定为 `%LOCALAPPDATA%\QingLi\qingli.db`。
- 第一阶段不得修改系统时钟策略、不得注入 Explorer、不得修改系统文件。
- 所有中文界面文字统一使用 UTF-8 源文件。

---

## File Map

```text
QingLi.sln
Directory.Build.props                         全局编译与分析器设置
src/QingLi.Core/                              纯业务逻辑
  Calendars/CalendarDay.cs
  Calendars/CalendarMonthService.cs
  Calendars/LunarDate.cs
  Calendars/LunarCalendarService.cs
  Calendars/SolarTermService.cs
  Holidays/HolidayDefinition.cs
  Holidays/HolidayService.cs
  Birthdays/Birthday.cs
  Birthdays/BirthdayOccurrenceService.cs
  Reminders/ReminderCandidate.cs
  Reminders/ReminderPlanner.cs
  Settings/AppSettings.cs
src/QingLi.Infrastructure/                    SQLite 与文件数据
  Data/SqliteConnectionFactory.cs
  Data/DatabaseMigrator.cs
  Birthdays/SqliteBirthdayRepository.cs
  Reminders/SqliteReminderHistoryRepository.cs
  Settings/JsonSettingsStore.cs
  Holidays/JsonHolidayProvider.cs
src/QingLi.Windows/                           Windows 集成
  App.xaml
  App.xaml.cs
  Shell/SingleInstanceCoordinator.cs
  Tray/TrayIconService.cs
  Notifications/WindowsNotificationService.cs
  Startup/StartupTaskService.cs
  Scheduling/ReminderScheduler.cs
  Views/CalendarPopupWindow.xaml
  Views/BirthdayManagerWindow.xaml
  Views/BirthdayEditorWindow.xaml
  Views/SettingsWindow.xaml
  ViewModels/*.cs
  Assets/Holidays/cn-2026.json
src/QingLi.Package/                           MSIX 打包
tests/QingLi.Core.Tests/
tests/QingLi.Infrastructure.Tests/
tests/QingLi.Windows.Tests/
```

### Task 1: 建立可编译、可测试的解决方案骨架

**Files:**
- Create: `QingLi.sln`
- Create: `Directory.Build.props`
- Create: `src/QingLi.Core/QingLi.Core.csproj`
- Create: `src/QingLi.Infrastructure/QingLi.Infrastructure.csproj`
- Create: `src/QingLi.Windows/QingLi.Windows.csproj`
- Create: `tests/QingLi.Core.Tests/QingLi.Core.Tests.csproj`
- Create: `tests/QingLi.Core.Tests/SmokeTests.cs`

**Interfaces:**
- Produces: `QingLi.Core`, `QingLi.Infrastructure`, `QingLi.Windows` 三个项目及可运行的 xUnit 测试入口。

- [ ] **Step 1: 创建解决方案与项目**

Run:

```powershell
dotnet new sln -n QingLi
dotnet new classlib -n QingLi.Core -o src/QingLi.Core -f net8.0
dotnet new classlib -n QingLi.Infrastructure -o src/QingLi.Infrastructure -f net8.0
dotnet new wpf -n QingLi.Windows -o src/QingLi.Windows -f net8.0
dotnet new xunit -n QingLi.Core.Tests -o tests/QingLi.Core.Tests -f net8.0
dotnet sln add src/QingLi.Core src/QingLi.Infrastructure src/QingLi.Windows tests/QingLi.Core.Tests
dotnet add src/QingLi.Infrastructure reference src/QingLi.Core
dotnet add src/QingLi.Windows reference src/QingLi.Core src/QingLi.Infrastructure
dotnet add tests/QingLi.Core.Tests reference src/QingLi.Core
```

Expected: 每条命令退出码为 `0`，`QingLi.sln` 包含四个项目。

- [ ] **Step 2: 写入冒烟测试**

```csharp
using System.Reflection;

namespace QingLi.Core.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Core_assembly_has_expected_identity()
    {
        var assembly = typeof(QingLi.Core.AssemblyMarker).Assembly;

        Assert.Equal("QingLi.Core", assembly.GetName().Name);
        Assert.Equal(new Version(0, 1, 0), assembly.GetName().Version);
        Assert.Equal("QingLi.Core", assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product);
    }
}
```

`QingLi.Core.AssemblyMarker` 是空的公开标记类型，只用于提供稳定的程序集入口。`Directory.Build.props` 将 `Version` 固定为 `0.1.0`，将 `Product` 固定为当前项目名，并为所有项目启用 nullable、隐式 using、警告视为错误和确定性构建。

- [ ] **Step 3: 运行测试**

Run: `dotnet test QingLi.sln`

Expected: `Passed: 1, Failed: 0`。

- [ ] **Step 4: 提交**

```powershell
git add QingLi.sln Directory.Build.props src tests
git commit -m "build: scaffold QingLi solution"
```

### Task 2: 定义生日领域模型与公历发生日期

**Files:**
- Create: `src/QingLi.Core/Birthdays/Birthday.cs`
- Create: `src/QingLi.Core/Birthdays/BirthdayOccurrenceService.cs`
- Create: `tests/QingLi.Core.Tests/Birthdays/BirthdayOccurrenceServiceTests.cs`

**Interfaces:**
- Produces: `Birthday`, `BirthdayCalendarKind`, `BirthdayOccurrenceService.GetOccurrence(Birthday birthday, int year)`.

- [ ] **Step 1: 写公历生日失败测试**

```csharp
public sealed class BirthdayOccurrenceServiceTests
{
    [Fact]
    public void Gregorian_birthday_uses_same_month_and_day()
    {
        var birthday = new Birthday(Guid.NewGuid(), "小林",
            BirthdayCalendarKind.Gregorian, 1990, 8, 18, false, 3,
            new TimeOnly(9, 0), null, true);

        var actual = new BirthdayOccurrenceService().GetOccurrence(birthday, 2027);

        Assert.Equal(new DateOnly(2027, 8, 18), actual);
    }
}
```

- [ ] **Step 2: 验证测试因类型不存在而失败**

Run: `dotnet test tests/QingLi.Core.Tests --filter Gregorian_birthday_uses_same_month_and_day`

Expected: FAIL，错误包含 `Birthday could not be found`。

- [ ] **Step 3: 写最小实现**

```csharp
namespace QingLi.Core.Birthdays;

public enum BirthdayCalendarKind { Gregorian, Lunar }

public sealed record Birthday(
    Guid Id, string Name, BirthdayCalendarKind CalendarKind,
    int BirthYear, int Month, int Day, bool IsLeapMonth,
    int ReminderDaysBefore, TimeOnly ReminderTime,
    string? Notes, bool IsEnabled);

public sealed class BirthdayOccurrenceService
{
    public DateOnly GetOccurrence(Birthday birthday, int year)
    {
        if (birthday.CalendarKind != BirthdayCalendarKind.Gregorian)
            throw new NotSupportedException("Lunar birthdays are implemented separately.");

        var day = Math.Min(birthday.Day, DateTime.DaysInMonth(year, birthday.Month));
        return new DateOnly(year, birthday.Month, day);
    }
}
```

- [ ] **Step 4: 增加 2 月 29 日回退测试并运行**

```csharp
[Fact]
public void Gregorian_february_29_falls_back_to_last_day_in_non_leap_year()
{
    var birthday = new Birthday(Guid.NewGuid(), "小周",
        BirthdayCalendarKind.Gregorian, 2000, 2, 29, false, 0,
        new TimeOnly(8, 0), null, true);

    Assert.Equal(new DateOnly(2027, 2, 28),
        new BirthdayOccurrenceService().GetOccurrence(birthday, 2027));
}
```

Run: `dotnet test tests/QingLi.Core.Tests`

Expected: 所有测试 PASS。

- [ ] **Step 5: 提交**

```powershell
git add src/QingLi.Core/Birthdays tests/QingLi.Core.Tests/Birthdays
git commit -m "feat: calculate Gregorian birthday occurrences"
```

### Task 3: 实现农历换算、闰月与月末回退

**Files:**
- Create: `src/QingLi.Core/Calendars/LunarDate.cs`
- Create: `src/QingLi.Core/Calendars/LunarCalendarService.cs`
- Modify: `src/QingLi.Core/Birthdays/BirthdayOccurrenceService.cs`
- Create: `tests/QingLi.Core.Tests/Calendars/LunarCalendarServiceTests.cs`
- Modify: `tests/QingLi.Core.Tests/Birthdays/BirthdayOccurrenceServiceTests.cs`

**Interfaces:**
- Produces: `LunarDate`, `LunarCalendarService.FromGregorian(DateOnly)`, `LunarCalendarService.ToGregorian(int, int, int, bool)`.
- Consumes: `Birthday` from Task 2.

- [ ] **Step 1: 写已知日期双向换算失败测试**

```csharp
[Theory]
[InlineData(2026, 2, 17, 2026, 1, 1, false)]
[InlineData(2026, 9, 25, 2026, 8, 15, false)]
public void Converts_known_dates(
    int gy, int gm, int gd, int ly, int lm, int ld, bool leap)
{
    var service = new LunarCalendarService();
    var lunar = service.FromGregorian(new DateOnly(gy, gm, gd));
    Assert.Equal(new LunarDate(ly, lm, ld, leap), lunar);
    Assert.Equal(new DateOnly(gy, gm, gd), service.ToGregorian(ly, lm, ld, leap));
}
```

- [ ] **Step 2: 验证失败**

Run: `dotnet test tests/QingLi.Core.Tests --filter Converts_known_dates`

Expected: FAIL，类型 `LunarCalendarService` 不存在。

- [ ] **Step 3: 使用 `ChineseLunisolarCalendar` 写最小双向实现**

```csharp
public sealed record LunarDate(int Year, int Month, int Day, bool IsLeapMonth);

public sealed class LunarCalendarService
{
    private readonly ChineseLunisolarCalendar _calendar = new();

    public LunarDate FromGregorian(DateOnly date)
    {
        var value = date.ToDateTime(TimeOnly.MinValue);
        var year = _calendar.GetYear(value);
        var rawMonth = _calendar.GetMonth(value);
        var leap = _calendar.GetLeapMonth(year);
        var isLeap = leap > 0 && rawMonth == leap;
        var month = leap > 0 && rawMonth >= leap ? rawMonth - 1 : rawMonth;
        return new LunarDate(year, month, _calendar.GetDayOfMonth(value), isLeap);
    }

    public DateOnly ToGregorian(int year, int month, int day, bool isLeapMonth)
    {
        var leap = _calendar.GetLeapMonth(year);
        var rawMonth = month;
        if (leap > 0 && (month >= leap || isLeapMonth)) rawMonth++;
        if (isLeapMonth && leap != rawMonth)
            throw new ArgumentOutOfRangeException(nameof(isLeapMonth));
        var maxDay = _calendar.GetDaysInMonth(year, rawMonth);
        var value = _calendar.ToDateTime(year, rawMonth, Math.Min(day, maxDay), 0, 0, 0, 0);
        return DateOnly.FromDateTime(value);
    }
}
```

- [ ] **Step 4: 写“目标年无对应闰月时用普通月”测试**

```csharp
[Fact]
public void Leap_month_birthday_falls_back_to_regular_month_when_absent()
{
    var birthday = new Birthday(Guid.NewGuid(), "小夏",
        BirthdayCalendarKind.Lunar, 1990, 4, 30, true, 1,
        new TimeOnly(9, 0), null, true);

    var date = new BirthdayOccurrenceService(new LunarCalendarService())
        .GetOccurrence(birthday, 2027);

    Assert.Equal(new LunarDate(2027, 4, 30, false),
        new LunarCalendarService().FromGregorian(date));
}
```

- [ ] **Step 5: 扩展生日服务并运行全部测试**

```csharp
public sealed class BirthdayOccurrenceService(LunarCalendarService? lunar = null)
{
    private readonly LunarCalendarService _lunar = lunar ?? new();

    public DateOnly GetOccurrence(Birthday birthday, int year)
    {
        if (birthday.CalendarKind == BirthdayCalendarKind.Gregorian)
            return new DateOnly(year, birthday.Month,
                Math.Min(birthday.Day, DateTime.DaysInMonth(year, birthday.Month)));

        try
        {
            return _lunar.ToGregorian(year, birthday.Month, birthday.Day, birthday.IsLeapMonth);
        }
        catch (ArgumentOutOfRangeException) when (birthday.IsLeapMonth)
        {
            return _lunar.ToGregorian(year, birthday.Month, birthday.Day, false);
        }
    }
}
```

Run: `dotnet test QingLi.sln`

Expected: 所有测试 PASS。

- [ ] **Step 6: 提交**

```powershell
git add src/QingLi.Core tests/QingLi.Core.Tests
git commit -m "feat: support lunar birthday conversion"
```

### Task 4: 生成月历、节气与法定节假日

**Files:**
- Create: `src/QingLi.Core/Calendars/CalendarDay.cs`
- Create: `src/QingLi.Core/Calendars/CalendarMonthService.cs`
- Create: `src/QingLi.Core/Calendars/SolarTermService.cs`
- Create: `src/QingLi.Core/Holidays/HolidayDefinition.cs`
- Create: `src/QingLi.Core/Holidays/HolidayService.cs`
- Create: `src/QingLi.Infrastructure/Holidays/JsonHolidayProvider.cs`
- Create: `src/QingLi.Windows/Assets/Holidays/cn-2026.json`
- Create: `tests/QingLi.Core.Tests/Calendars/CalendarMonthServiceTests.cs`
- Create: `tests/QingLi.Infrastructure.Tests/Holidays/JsonHolidayProviderTests.cs`

**Interfaces:**
- Produces: `CalendarMonthService.Build(int year, int month, DayOfWeek firstDay)`.
- Produces: `CalendarDay(DateOnly Date, LunarDate Lunar, string? SolarTerm, HolidayDefinition? Holiday, bool IsCurrentMonth)`.

- [ ] **Step 1: 写固定 42 格月历失败测试**

```csharp
[Fact]
public void Month_grid_contains_42_days_and_starts_on_configured_weekday()
{
    var days = TestServices.CalendarMonth().Build(2026, 6, DayOfWeek.Monday);
    Assert.Equal(42, days.Count);
    Assert.Equal(DayOfWeek.Monday, days[0].Date.DayOfWeek);
    Assert.Contains(days, x => x.Date == new DateOnly(2026, 6, 24) && x.IsCurrentMonth);
}
```

- [ ] **Step 2: 验证失败后实现月历组合**

```csharp
public sealed class CalendarMonthService(
    LunarCalendarService lunar,
    SolarTermService solarTerms,
    HolidayService holidays)
{
    public IReadOnlyList<CalendarDay> Build(int year, int month, DayOfWeek firstDay)
    {
        var first = new DateOnly(year, month, 1);
        var offset = ((int)first.DayOfWeek - (int)firstDay + 7) % 7;
        var start = first.AddDays(-offset);
        return Enumerable.Range(0, 42).Select(index =>
        {
            var date = start.AddDays(index);
            return new CalendarDay(date, lunar.FromGregorian(date),
                solarTerms.GetName(date), holidays.Find(date), date.Month == month);
        }).ToArray();
    }
}
```

- [ ] **Step 3: 写 2026 节假日 JSON 解析测试**

```csharp
[Fact]
public async Task Reads_holiday_and_makeup_workday()
{
    var provider = new JsonHolidayProvider();
    var package = await provider.ReadAsync("Fixtures/cn-2026.json");
    Assert.Contains(package.Days, x => x.Name == "国庆节" && !x.IsWorkday);
    Assert.Contains(package.Days, x => x.IsWorkday);
}
```

- [ ] **Step 4: 实现严格 JSON 模型和 2026 数据包**

```json
{
  "country": "CN",
  "year": 2026,
  "version": "2026.1",
  "days": [
    { "date": "2026-01-01", "name": "元旦", "isWorkday": false }
  ]
}
```

节假日完整日期必须来自国务院发布的 2026 年放假安排；实现时逐项录入并在测试中校验包的年份、日期唯一性和至少一个调休工作日。

- [ ] **Step 5: 实现二十四节气表驱动算法并测试已知日期**

```csharp
[Theory]
[InlineData(2026, 2, 4, "立春")]
[InlineData(2026, 6, 21, "夏至")]
public void Returns_known_solar_terms(int year, int month, int day, string name)
{
    Assert.Equal(name, new SolarTermService().GetName(new DateOnly(year, month, day)));
}
```

使用 1901—2100 年按年生成并固化的节气数据表；不要在 UI 中做天文计算。生成脚本与来源说明保存到 `tools/SolarTerms/README.md`，运行时只读取内嵌表。

- [ ] **Step 6: 运行全部测试并提交**

Run: `dotnet test QingLi.sln`

Expected: 所有测试 PASS。

```powershell
git add src tests tools
git commit -m "feat: build lunar holiday calendar months"
```

### Task 5: 建立 SQLite 数据库与生日仓储

**Files:**
- Create: `src/QingLi.Infrastructure/Data/SqliteConnectionFactory.cs`
- Create: `src/QingLi.Infrastructure/Data/DatabaseMigrator.cs`
- Create: `src/QingLi.Core/Birthdays/IBirthdayRepository.cs`
- Create: `src/QingLi.Infrastructure/Birthdays/SqliteBirthdayRepository.cs`
- Create: `tests/QingLi.Infrastructure.Tests/Birthdays/SqliteBirthdayRepositoryTests.cs`
- Create: `tests/QingLi.Infrastructure.Tests/Data/DatabaseRecoveryTests.cs`

**Interfaces:**
- Produces: `IBirthdayRepository.ListAsync`, `GetAsync`, `SaveAsync`, `DeleteAsync`.

- [ ] **Step 1: 写往返保存失败测试**

```csharp
[Fact]
public async Task Saves_and_reads_birthday()
{
    await using var database = await TestDatabase.CreateAsync();
    var repository = new SqliteBirthdayRepository(database.Factory);
    var birthday = BirthdaySamples.Lunar();

    await repository.SaveAsync(birthday, CancellationToken.None);
    var actual = await repository.GetAsync(birthday.Id, CancellationToken.None);

    Assert.Equal(birthday, actual);
}
```

- [ ] **Step 2: 验证失败**

Run: `dotnet test tests/QingLi.Infrastructure.Tests --filter Saves_and_reads_birthday`

Expected: FAIL，仓储类型不存在。

- [ ] **Step 3: 创建迁移和参数化 UPSERT**

```sql
CREATE TABLE IF NOT EXISTS birthdays (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  calendar_kind INTEGER NOT NULL,
  birth_year INTEGER NOT NULL,
  month INTEGER NOT NULL,
  day INTEGER NOT NULL,
  is_leap_month INTEGER NOT NULL,
  reminder_days_before INTEGER NOT NULL,
  reminder_time TEXT NOT NULL,
  notes TEXT NULL,
  is_enabled INTEGER NOT NULL
);
```

`SaveAsync` 必须开启事务，使用参数化 `INSERT ... ON CONFLICT(id) DO UPDATE`，提交后才返回。

- [ ] **Step 4: 增加删除、按姓名搜索和最近生日排序测试**

```csharp
[Fact]
public async Task Delete_removes_only_selected_birthday()
{
    await using var database = await TestDatabase.CreateAsync();
    var repository = new SqliteBirthdayRepository(database.Factory);
    var first = BirthdaySamples.Gregorian("甲");
    var second = BirthdaySamples.Gregorian("乙");
    await repository.SaveAsync(first, default);
    await repository.SaveAsync(second, default);

    await repository.DeleteAsync(first.Id, default);

    Assert.Null(await repository.GetAsync(first.Id, default));
    Assert.NotNull(await repository.GetAsync(second.Id, default));
}
```

- [ ] **Step 5: 增加数据库损坏保护测试**

```csharp
[Fact]
public async Task Corrupt_database_is_preserved_and_not_overwritten()
{
    var path = Path.Combine(_temp, "qingli.db");
    await File.WriteAllTextAsync(path, "not-a-sqlite-database");
    var migrator = new DatabaseMigrator(new SqliteConnectionFactory(path));

    var result = await migrator.TryMigrateAsync(default);

    Assert.False(result.IsWritable);
    Assert.Equal("not-a-sqlite-database", await File.ReadAllTextAsync(path));
    Assert.True(File.Exists(result.PreservedCopyPath));
}
```

迁移失败时复制原文件为带时间戳的 `.corrupt-copy`，尝试 SQLite 只读连接；不得删除、截断或自动新建覆盖原数据库。

- [ ] **Step 6: 运行测试并提交**

Run: `dotnet test QingLi.sln`

Expected: 所有测试 PASS。

```powershell
git add src/QingLi.Core/Birthdays src/QingLi.Infrastructure tests/QingLi.Infrastructure.Tests
git commit -m "feat: persist birthdays in SQLite"
```

### Task 6: 实现提醒计划、去重和唤醒补查

**Files:**
- Create: `src/QingLi.Core/Reminders/ReminderCandidate.cs`
- Create: `src/QingLi.Core/Reminders/ReminderPlanner.cs`
- Create: `src/QingLi.Core/Reminders/IReminderHistoryRepository.cs`
- Create: `src/QingLi.Core/Reminders/ReminderSuppression.cs`
- Create: `src/QingLi.Infrastructure/Reminders/SqliteReminderHistoryRepository.cs`
- Create: `src/QingLi.Windows/Scheduling/ReminderScheduler.cs`
- Create: `tests/QingLi.Core.Tests/Reminders/ReminderPlannerTests.cs`
- Create: `tests/QingLi.Windows.Tests/Scheduling/ReminderSchedulerTests.cs`

**Interfaces:**
- Produces: `ReminderPlanner.DueBetween(IReadOnlyList<Birthday>, DateTimeOffset from, DateTimeOffset to)`.
- Produces: `ReminderScheduler.CheckAsync(DateTimeOffset now, CancellationToken)`.

- [ ] **Step 1: 写提前三天提醒失败测试**

```csharp
[Fact]
public void Plans_reminder_at_configured_lead_time()
{
    var birthday = BirthdaySamples.Gregorian("小林", month: 8, day: 18,
        daysBefore: 3, time: new TimeOnly(9, 0));
    var planner = new ReminderPlanner(new BirthdayOccurrenceService());

    var result = planner.DueBetween([birthday],
        new DateTimeOffset(2027, 8, 15, 8, 59, 0, TimeSpan.FromHours(8)),
        new DateTimeOffset(2027, 8, 15, 9, 1, 0, TimeSpan.FromHours(8)));

    Assert.Single(result);
    Assert.Equal(new DateOnly(2027, 8, 18), result[0].OccurrenceDate);
}
```

- [ ] **Step 2: 实现候选提醒生成**

```csharp
public sealed record ReminderCandidate(
    Guid BirthdayId, string Name, DateOnly OccurrenceDate,
    DateTimeOffset ScheduledAt);

public IReadOnlyList<ReminderCandidate> DueBetween(
    IReadOnlyList<Birthday> birthdays, DateTimeOffset from, DateTimeOffset to)
{
    return birthdays.Where(x => x.IsEnabled).SelectMany(birthday =>
    {
        var occurrence = _occurrences.GetOccurrence(birthday, to.Year);
        var local = occurrence.AddDays(-birthday.ReminderDaysBefore)
            .ToDateTime(birthday.ReminderTime);
        var scheduled = new DateTimeOffset(local, to.Offset);
        return scheduled > from && scheduled <= to
            ? [new ReminderCandidate(birthday.Id, birthday.Name, occurrence, scheduled)]
            : Array.Empty<ReminderCandidate>();
    }).ToArray();
}
```

- [ ] **Step 3: 写补查与去重测试**

```csharp
[Fact]
public async Task Wake_check_sends_due_reminder_once()
{
    var clock = new FakeClock("2027-08-15T10:00:00+08:00");
    var scheduler = SchedulerFixture.Create(clock, lastCheck: "2027-08-15T08:00:00+08:00");

    await scheduler.CheckAsync(clock.Now, default);
    await scheduler.CheckAsync(clock.Now, default);

    Assert.Single(scheduler.NotificationSink.Sent);
}
```

- [ ] **Step 4: 实现调度规则**

调度器每分钟检查一次；同时订阅 `SystemEvents.PowerModeChanged` 和系统时间变化。查询区间从上次成功检查到当前时刻；若提醒日期仍是今天则补发，早于今天则丢弃；发送成功后在同一事务中写入唯一键 `(birthday_id, scheduled_at)`。

- [ ] **Step 5: 写并实现“今天不再提醒”测试**

```csharp
[Fact]
public async Task Suppress_today_skips_only_candidates_scheduled_today()
{
    var fixture = SchedulerFixture.Create();
    await fixture.Suppression.SuppressAsync(new DateOnly(2027, 8, 15), default);
    await fixture.Scheduler.CheckAsync(
        new DateTimeOffset(2027, 8, 15, 10, 0, 0, TimeSpan.FromHours(8)), default);
    Assert.Empty(fixture.NotificationSink.Sent);
    Assert.True(fixture.Birthday.IsEnabled);
}
```

抑制状态按本地日期存入 `settings`，跨日自动失效，不修改任何生日记录。

- [ ] **Step 6: 运行测试并提交**

Run: `dotnet test QingLi.sln`

Expected: 所有测试 PASS，重复检查只发送一次。

```powershell
git add src tests
git commit -m "feat: schedule and deduplicate birthday reminders"
```

### Task 7: 实现设置、开机启动和单实例

**Files:**
- Create: `src/QingLi.Core/Settings/AppSettings.cs`
- Create: `src/QingLi.Infrastructure/Settings/JsonSettingsStore.cs`
- Create: `src/QingLi.Windows/Shell/SingleInstanceCoordinator.cs`
- Create: `src/QingLi.Windows/Startup/StartupTaskService.cs`
- Create: `tests/QingLi.Infrastructure.Tests/Settings/JsonSettingsStoreTests.cs`
- Create: `tests/QingLi.Windows.Tests/Shell/SingleInstanceCoordinatorTests.cs`

**Interfaces:**
- Produces: `ISettingsStore.LoadAsync/SaveAsync`.
- Produces: `SingleInstanceCoordinator.TryAcquireAsync()` 与 `ActivationRequested`。

- [ ] **Step 1: 写设置默认值与往返测试**

```csharp
[Fact]
public async Task Missing_settings_return_safe_defaults()
{
    var store = new JsonSettingsStore(Path.Combine(_temp, "settings.json"));
    var settings = await store.LoadAsync(default);
    Assert.Equal(AppTheme.System, settings.Theme);
    Assert.Equal(DayOfWeek.Monday, settings.FirstDayOfWeek);
    Assert.False(settings.StartWithWindows);
}
```

- [ ] **Step 2: 实现原子设置写入**

将 JSON 写到同目录临时文件，调用 `File.Move(temp, target, true)` 替换；反序列化失败时保留损坏文件并返回默认值。

- [ ] **Step 3: 写第二实例激活测试**

```csharp
[Fact]
public async Task Second_instance_notifies_first_instance()
{
    await using var first = new SingleInstanceCoordinator("QingLi.Test");
    Assert.True(await first.TryAcquireAsync());
    var signal = first.WaitForActivationAsync();
    await using var second = new SingleInstanceCoordinator("QingLi.Test");
    Assert.False(await second.TryAcquireAsync());
    await second.SignalPrimaryAsync("show-calendar");
    Assert.Equal("show-calendar", await signal.WaitAsync(TimeSpan.FromSeconds(2)));
}
```

- [ ] **Step 4: 使用命名互斥量和命名管道实现单实例**

主实例持有 `Local\QingLi.SingleInstance`；第二实例通过 `QingLi.Activation` 命名管道发送 UTF-8 命令并退出。

- [ ] **Step 5: 实现当前用户开机启动**

使用注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，值名 `QingLi`，值为带引号的当前可执行文件路径。关闭设置时只删除本应用的值。

- [ ] **Step 6: 运行测试并提交**

Run: `dotnet test QingLi.sln`

Expected: 所有测试 PASS。

```powershell
git add src tests
git commit -m "feat: add local settings startup and single instance"
```

### Task 8: 建立托盘入口和月历弹窗

**Files:**
- Create: `src/QingLi.Windows/Tray/TrayIconService.cs`
- Create: `src/QingLi.Windows/Views/CalendarPopupWindow.xaml`
- Create: `src/QingLi.Windows/ViewModels/CalendarPopupViewModel.cs`
- Modify: `src/QingLi.Windows/App.xaml.cs`
- Create: `tests/QingLi.Windows.Tests/ViewModels/CalendarPopupViewModelTests.cs`

**Interfaces:**
- Consumes: `CalendarMonthService.Build`.
- Consumes: `IBirthdayRepository.ListAsync`，将生日发生日期合并为日历格标记和选中日期详情。
- Produces: `CalendarPopupViewModel.LoadMonthAsync`, `PreviousMonthCommand`, `NextMonthCommand`, `TodayCommand`.

- [ ] **Step 1: 写视图模型月份导航失败测试**

```csharp
[Fact]
public async Task Next_month_rebuilds_calendar()
{
    var vm = CalendarPopupFixture.Create(today: new DateOnly(2026, 6, 24));
    await vm.InitializeAsync();
    vm.NextMonthCommand.Execute(null);
    Assert.Equal(7, vm.DisplayMonth.Month);
    Assert.Equal(42, vm.Days.Count);
}
```

- [ ] **Step 2: 实现视图模型**

视图模型公开 `ObservableCollection<CalendarDayViewModel> Days`、`DateOnly DisplayMonth`、`CalendarDayViewModel? SelectedDay`。命令只改变月份并调用业务服务，不在 ViewModel 重新计算农历。加载月份时查询启用的生日，以 `BirthdayOccurrenceService` 计算当年发生日期，并填充每格的 `Birthdays` 集合；选中日期详情显示姓名列表。

- [ ] **Step 3: 实现 Windows 11 风格弹窗**

窗口要求：

```xml
<Window WindowStyle="None"
        ResizeMode="NoResize"
        ShowInTaskbar="False"
        Width="380"
        Height="520"
        AllowsTransparency="True"
        Background="Transparent">
```

内部使用圆角卡片、7 列 `ItemsControl` 月历、日期详情区；按 `Esc` 或失去激活关闭。日历打开时根据 `SystemParameters.WorkArea` 放在任务栏上方。

- [ ] **Step 4: 实现托盘菜单**

托盘左键切换日历；右键菜单固定为“打开日历、添加生日、设置、暂停今日提醒、退出”。应用启动后不显示主窗口。

- [ ] **Step 5: 运行自动测试与手工冒烟**

Run:

```powershell
dotnet test QingLi.sln
dotnet run --project src/QingLi.Windows
```

Expected: 测试全绿；托盘图标出现；左键弹出月历；点击窗口外关闭。

- [ ] **Step 6: 提交**

```powershell
git add src/QingLi.Windows tests/QingLi.Windows.Tests
git commit -m "feat: add tray calendar popup"
```

### Task 9: 完成生日管理和设置界面

**Files:**
- Create: `src/QingLi.Windows/Views/BirthdayManagerWindow.xaml`
- Create: `src/QingLi.Windows/Views/BirthdayEditorWindow.xaml`
- Create: `src/QingLi.Windows/ViewModels/BirthdayManagerViewModel.cs`
- Create: `src/QingLi.Windows/ViewModels/BirthdayEditorViewModel.cs`
- Create: `src/QingLi.Windows/Views/SettingsWindow.xaml`
- Create: `src/QingLi.Windows/ViewModels/SettingsViewModel.cs`
- Create: `tests/QingLi.Windows.Tests/ViewModels/BirthdayEditorViewModelTests.cs`

**Interfaces:**
- Consumes: `IBirthdayRepository`, `ISettingsStore`.
- Produces: 可验证的新增、编辑、删除、搜索、设置保存命令。

- [ ] **Step 1: 写输入校验失败测试**

```csharp
[Theory]
[InlineData("", 8, 18, "请输入姓名")]
[InlineData("小林", 13, 18, "月份应在 1 到 12 之间")]
[InlineData("小林", 8, 31, "日期超出范围")]
public void Invalid_input_has_clear_message(
    string name, int month, int day, string expected)
{
    var vm = BirthdayEditorFixture.Create(name, month, day);
    Assert.Contains(expected, vm.Validate());
}
```

- [ ] **Step 2: 实现编辑视图模型与保存命令**

保存前校验姓名、月份、日期、提前天数 `0..365` 和提醒时间。公历按实际年月校验；农历允许 `1..30`，最终由农历服务确认。校验失败不写数据库。

- [ ] **Step 3: 实现管理列表**

列表显示姓名、日期类型、生日、下次发生日期和提醒规则；支持姓名搜索和最近生日排序；删除前显示二次确认。

- [ ] **Step 4: 实现设置页**

包含自动/浅色/深色主题、12/24 小时时间格式、日期格式、时钟字号、时钟文字颜色、一周首日、开机启动、打开数据目录。系统处于高对比度模式时优先使用系统颜色并禁用自定义文字颜色。此阶段“替换系统时钟”显示为“下一阶段提供”，不可操作。

- [ ] **Step 5: 运行测试与手工验收**

Run: `dotnet test QingLi.sln`

Expected: 全部 PASS；手工新增公历与农历生日后重启应用，数据仍存在。

- [ ] **Step 6: 提交**

```powershell
git add src/QingLi.Windows tests/QingLi.Windows.Tests
git commit -m "feat: add birthday and settings screens"
```

### Task 10: 接入 Windows 通知

**Files:**
- Create: `src/QingLi.Core/Reminders/INotificationService.cs`
- Create: `src/QingLi.Windows/Notifications/WindowsNotificationService.cs`
- Modify: `src/QingLi.Windows/App.xaml.cs`
- Create: `src/QingLi.Package/Package.appxmanifest`
- Create: `tests/QingLi.Windows.Tests/Notifications/NotificationPayloadTests.cs`

**Interfaces:**
- Produces: `INotificationService.ShowBirthdayAsync(ReminderCandidate, CancellationToken)`.
- Consumes: `ReminderCandidate` from Task 6.

- [ ] **Step 1: 写通知内容测试**

```csharp
[Fact]
public void Birthday_payload_contains_name_date_and_action()
{
    var candidate = ReminderSamples.For("小林", new DateOnly(2027, 8, 18));
    var payload = NotificationPayloadBuilder.Build(candidate);
    Assert.Contains("小林", payload.Title);
    Assert.Contains("8月18日", payload.Body);
    Assert.Equal($"birthdayId={candidate.BirthdayId}", payload.Arguments);
}
```

- [ ] **Step 2: 实现负载构造器**

```csharp
public static NotificationPayload Build(ReminderCandidate value) =>
    new("生日提醒",
        $"{value.Name}的生日是 {value.OccurrenceDate: M月d日}",
        $"action=open-birthday&birthdayId={value.BirthdayId}");
```

- [ ] **Step 3: 使用 Windows App SDK 注册并发送通知**

应用以普通用户运行；启动时调用 `AppNotificationManager.Default.Register()`，退出时 `Unregister()`。通知包含“打开详情”和“今天不再提醒”，点击后经单实例激活通道打开对应生日。

- [ ] **Step 4: 配置 MSIX 通知激活**

在清单中声明 `windows.toastNotificationActivation` 和 COM 激活 GUID。安装包身份固定为 `QingLi.Calendar`，显示名为“轻历”。

- [ ] **Step 5: 验证**

Run:

```powershell
dotnet test QingLi.sln
dotnet run --project src/QingLi.Windows
```

Expected: 自动测试全绿；测试生日到期时 Windows 11 显示通知；点击后打开生日详情。以管理员身份启动时显示“通知功能需要普通用户运行”，不静默假成功。

- [ ] **Step 6: 提交**

```powershell
git add src tests
git commit -m "feat: send actionable birthday notifications"
```

### Task 11: 打包、隐私说明和核心版验收

**Files:**
- Create: `README.md`
- Create: `docs/PRIVACY.md`
- Create: `docs/TESTING.md`
- Create: `scripts/package.ps1`
- Modify: `src/QingLi.Package/Package.appxmanifest`

**Interfaces:**
- Produces: `artifacts/QingLi-<version>-x64.msix`。

- [ ] **Step 1: 写打包脚本**

脚本先运行 `dotnet test QingLi.sln -c Release`，失败立即退出；随后执行 x64 自包含发布和 MSIX 打包，产物放入 `artifacts`。

- [ ] **Step 2: 写用户文档**

README 必须说明：纯本地、无会员、Windows 11 x64、生日数据目录、卸载前后数据行为。隐私说明明确“不上传姓名、生日、备注或使用记录”。

- [ ] **Step 3: 执行完整验证**

Run:

```powershell
dotnet test QingLi.sln -c Release
dotnet publish src/QingLi.Windows -c Release -r win-x64 --self-contained true
powershell -ExecutionPolicy Bypass -File scripts/package.ps1
```

Expected: 测试 `0 failed`；生成 x64 安装包。

- [ ] **Step 4: 执行手工验收**

- 全新安装后无需联网即可打开月历。
- 新增公历和农历生日，重启后仍存在。
- 到期提醒只出现一次。
- 休眠跨过提醒时间后，当天唤醒会补发。
- 托盘菜单、主题切换和开机启动有效。
- 卸载不删除 `%LOCALAPPDATA%\QingLi\qingli.db`，再次安装可继续使用。

- [ ] **Step 5: 提交**

```powershell
git add README.md docs scripts src/QingLi.Package
git commit -m "build: package QingLi core release"
```

## Completion Gate

核心版只有在以下命令输出无失败后才算完成：

```powershell
dotnet test QingLi.sln -c Release
powershell -ExecutionPolicy Bypass -File scripts/package.ps1
```

随后进入《轻历任务栏时钟替换 Implementation Plan》，不得提前将系统策略修改混入核心版。
