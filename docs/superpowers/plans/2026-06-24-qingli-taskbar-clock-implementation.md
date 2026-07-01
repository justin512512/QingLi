# 轻历任务栏时钟替换 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在核心版稳定运行的基础上，为 Windows 11 增加可选、可恢复的系统时钟隐藏和自定义任务栏时钟。

**Architecture:** 使用独立的系统策略服务保存并修改当前用户 `HideClock` 状态，自定义 WPF 无边框窗口只负责显示和点击交互，任务栏定位服务负责多显示器与 DPI。任何失败都退回托盘模式，恢复功能独立于主窗口可执行。

**Tech Stack:** C# 12、.NET 8、WPF、Win32 API、Windows Registry、xUnit、现有 QingLi Core/Infrastructure/Windows 项目。

## Global Constraints

- 必须先完成并验证核心版计划。
- 不向 Explorer 注入代码，不修改 Windows 系统文件。
- 替换模式默认关闭，只能由用户主动开启。
- 修改前必须记录原注册表值及其是否存在。
- 关闭替换模式、异常恢复和卸载时必须恢复原状态。
- 不支持时保留托盘模式并给出明确提示。
- 所有系统修改限定为当前用户范围。

---

### Task 1: 建立系统时钟状态快照与恢复服务

**Files:**
- Create: `src/QingLi.Core/ClockReplacement/SystemClockState.cs`
- Create: `src/QingLi.Core/ClockReplacement/ISystemClockPolicy.cs`
- Create: `src/QingLi.Windows/ClockReplacement/WindowsSystemClockPolicy.cs`
- Create: `src/QingLi.Infrastructure/ClockReplacement/SystemClockStateStore.cs`
- Create: `tests/QingLi.Windows.Tests/ClockReplacement/WindowsSystemClockPolicyTests.cs`

**Interfaces:**
- Produces: `CaptureAsync`, `HideAsync`, `RestoreAsync(SystemClockState)`.
- Produces: `SystemClockState(bool ValueExisted, int? OriginalValue, DateTimeOffset CapturedAt)`.

- [x] **Step 1: 写恢复“不存在的原值”失败测试**

```csharp
[Fact]
public async Task Restore_deletes_value_when_it_did_not_exist_before()
{
    var registry = new FakeUserRegistry();
    var policy = new WindowsSystemClockPolicy(registry, new FakeShellRefresh());
    var state = new SystemClockState(false, null, DateTimeOffset.UtcNow);

    await policy.HideAsync(default);
    await policy.RestoreAsync(state, default);

    Assert.False(registry.ValueExists("HideClock"));
}
```

- [x] **Step 2: 实现当前用户策略读写**

路径固定为：

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer
Value: HideClock
```

`HideAsync` 写入 DWORD `1`；恢复时若原值不存在则删除，存在则原样写回。每次更改后广播 `WM_SETTINGCHANGE`，不得结束 Explorer 进程。

- [x] **Step 3: 写状态持久化测试**

状态以 JSON 保存到 `%LOCALAPPDATA%\QingLi\system-clock-state.json`，必须先持久化快照，再修改策略；恢复成功后删除快照。

- [x] **Step 4: 运行测试并提交**

Run: `dotnet test QingLi.sln --filter ClockReplacement`

Expected: 所有时钟策略测试 PASS。

```powershell
git add src tests
git commit -m "feat: safely hide and restore system clock"
```

### Task 2: 检测任务栏、显示器与 DPI

**Files:**
- Create: `src/QingLi.Windows/ClockReplacement/TaskbarGeometry.cs`
- Create: `src/QingLi.Windows/ClockReplacement/TaskbarLocator.cs`
- Create: `src/QingLi.Windows/Interop/Shell32.cs`
- Create: `src/QingLi.Windows/Interop/User32.cs`
- Create: `tests/QingLi.Windows.Tests/ClockReplacement/TaskbarLocatorTests.cs`

**Interfaces:**
- Produces: `TaskbarLocator.GetPrimary()` 和 `GetForPoint(Point screenPoint)`.
- Produces: `TaskbarGeometry(Rect Bounds, TaskbarEdge Edge, double DpiScale)`.

- [x] **Step 1: 写底部任务栏定位测试**

```csharp
[Fact]
public void Bottom_taskbar_places_clock_inside_right_edge()
{
    var geometry = new TaskbarGeometry(
        new Rect(0, 1040, 1920, 40), TaskbarEdge.Bottom, 1.0);
    var bounds = ClockWindowPlacement.Calculate(geometry, new Size(104, 40));
    Assert.Equal(new Rect(1816, 1040, 104, 40), bounds);
}
```

- [x] **Step 2: 实现纯函数布局**

底部和顶部任务栏使用水平两行时钟；若检测到非底部布局则返回 `Unsupported`，保留托盘模式。尺寸按物理像素和 DPI 换算，禁止混用设备无关像素。

- [x] **Step 3: 使用 `SHAppBarMessage(ABM_GETTASKBARPOS)` 获取主任务栏**

无法获取、矩形无效或高度异常时返回失败结果，不猜测位置。显示变化后重新查询。

- [x] **Step 4: 运行测试并提交**

Run: `dotnet test QingLi.sln --filter Taskbar`

Expected: 定位与 DPI 测试全部 PASS。

```powershell
git add src tests
git commit -m "feat: locate Windows taskbar geometry"
```

### Task 3: 实现自定义任务栏时钟窗口

**Files:**
- Create: `src/QingLi.Windows/Views/TaskbarClockWindow.xaml`
- Create: `src/QingLi.Windows/ViewModels/TaskbarClockViewModel.cs`
- Create: `src/QingLi.Windows/ClockReplacement/ClockWindowController.cs`
- Create: `tests/QingLi.Windows.Tests/ViewModels/TaskbarClockViewModelTests.cs`

**Interfaces:**
- Produces: `TaskbarClockViewModel.Update(DateTimeOffset now, AppSettings settings)`.
- Produces: `ClockWindowController.ShowAsync`, `RepositionAsync`, `Hide`.

- [x] **Step 1: 写显示格式测试**

```csharp
[Theory]
[InlineData(false, "21:05")]
[InlineData(true, "9:05 PM")]
public void Formats_time(bool useTwelveHour, string expected)
{
    var vm = new TaskbarClockViewModel();
    vm.Update(new DateTimeOffset(2026, 6, 24, 21, 5, 0, TimeSpan.FromHours(8)),
        AppSettings.Default with { UseTwelveHourClock = useTwelveHour });
    Assert.Equal(expected, vm.TimeText);
    Assert.Equal("6月24日 周三", vm.DateText);
}
```

- [x] **Step 2: 增加日期格式、字号和颜色测试**

```csharp
[Fact]
public void Applies_user_clock_appearance()
{
    var vm = new TaskbarClockViewModel();
    vm.Update(Samples.Now, AppSettings.Default with
    {
        DateFormat = "M月d日 ddd",
        ClockFontSize = 12,
        ClockTextColor = "#FFF4F4F4"
    });
    Assert.Equal(12, vm.ClockFontSize);
    Assert.Equal("#FFF4F4F4", vm.ClockTextColor);
}
```

- [x] **Step 3: 实现无边框时钟窗口**

```xml
<Window WindowStyle="None"
        ResizeMode="NoResize"
        ShowInTaskbar="False"
        ShowActivated="False"
        Topmost="True"
        Background="Transparent">
```

窗口每秒更新时间；两行文本垂直居中；应用设置中的日期格式、字号和颜色；跟随主题，高对比度下改用系统颜色；点击调用现有日历弹窗。窗口不得抢占键盘焦点。

- [x] **Step 4: 连接定位控制器**

控制器只有在任务栏定位成功后才显示窗口。检测到位置越界、Explorer 重启或显示器变化时先隐藏，再重新定位。

- [ ] **Step 5: 运行测试与手工验证**

Run:

```powershell
dotnet test QingLi.sln
dotnet run --project src/QingLi.Windows
```

Expected: 测试全绿；测试开关打开后出现自定义时钟；点击打开日历；窗口不抢焦点。

- [x] **Step 6: 提交**

```powershell
git add src tests
git commit -m "feat: show custom taskbar clock window"
```

### Task 4: 编排启用、失败回滚和异常恢复

**Files:**
- Create: `src/QingLi.Windows/ClockReplacement/ClockReplacementCoordinator.cs`
- Modify: `src/QingLi.Windows/ViewModels/SettingsViewModel.cs`
- Modify: `src/QingLi.Windows/App.xaml.cs`
- Modify: `src/QingLi.Windows/Tray/TrayIconService.cs`
- Create: `tests/QingLi.Windows.Tests/ClockReplacement/ClockReplacementCoordinatorTests.cs`

**Interfaces:**
- Produces: `EnableAsync`, `DisableAsync`, `RecoverIfNeededAsync`.

- [x] **Step 1: 写“窗口创建失败则恢复系统时钟”测试**

```csharp
[Fact]
public async Task Enable_restores_system_clock_when_custom_clock_fails()
{
    var fixture = ClockReplacementFixture.WithClockWindowFailure();
    var result = await fixture.Coordinator.EnableAsync(default);
    Assert.False(result.IsSuccess);
    Assert.True(fixture.Policy.RestoreWasCalled);
    Assert.False(fixture.Settings.ClockReplacementEnabled);
}
```

- [x] **Step 2: 实现严格启用顺序**

1. 检测 Windows 11 和任务栏兼容性。
2. 保存系统时钟状态快照。
3. 创建并定位自定义时钟。
4. 隐藏系统时钟。
5. 保存 `ClockReplacementEnabled=true`。

任一步失败都逆序回滚；成功前不得把设置标记为启用。

- [x] **Step 3: 实现启动恢复**

若发现状态快照存在但设置未启用，说明上次过程未完成，立即恢复系统时钟。若设置启用但自定义窗口无法创建，也立即恢复并关闭替换模式。

- [x] **Step 4: 增加托盘恢复入口**

托盘菜单“恢复系统时钟”始终可见；点击后执行恢复、关闭自定义窗口并清除替换设置。

- [x] **Step 5: 运行测试并提交**

Run: `dotnet test QingLi.sln`

Expected: 所有测试 PASS，所有失败路径均调用恢复。

```powershell
git add src tests
git commit -m "feat: coordinate safe clock replacement"
```

### Task 5: 独立恢复工具、卸载恢复与兼容验收

**Files:**
- Create: `src/QingLi.Recovery/QingLi.Recovery.csproj`
- Create: `src/QingLi.Recovery/Program.cs`
- Modify: `QingLi.sln`
- Modify: `src/QingLi.Package/Package.appxmanifest`
- Create: `docs/CLOCK-RECOVERY.md`
- Modify: `scripts/package.ps1`

**Interfaces:**
- Produces: `QingLi.Recovery.exe --restore-clock`。

- [x] **Step 1: 建立恢复工具测试**

恢复逻辑复用 `WindowsSystemClockPolicy` 和 `SystemClockStateStore`；没有快照时删除轻历创建的 `HideClock=1`，但若检测到该值在安装前由用户策略明确存在，则不得删除。

- [x] **Step 2: 实现无 UI 恢复命令**

```csharp
var result = await recovery.RestoreAsync(CancellationToken.None);
Console.WriteLine(result.Message);
return result.IsSuccess ? 0 : 1;
```

恢复工具不启动主应用、不显示日历、不需要管理员权限。

- [x] **Step 3: 接入卸载前恢复**

安装包卸载动作调用恢复工具；文档同时给出手动命令：

```powershell
QingLi.Recovery.exe --restore-clock
```

- [ ] **Step 4: 完整兼容验证**

逐项验证：

- Windows 11、100%/125%/150%/200% 缩放。
- 单显示器与双显示器。
- Explorer 重启。
- 睡眠唤醒。
- 开启、关闭、应用崩溃后重启。
- 自定义时钟启动失败。
- 卸载及独立恢复工具。

每一种场景都必须确认系统时钟可以恢复，且托盘模式仍可用。

- [x] **Step 5: 发布验证**

Run:

```powershell
dotnet test QingLi.sln -c Release
powershell -ExecutionPolicy Bypass -File scripts/package.ps1
```

Expected: `0 failed`；安装包同时包含主程序与恢复工具。

- [x] **Step 6: 提交**

```powershell
git add QingLi.sln src/QingLi.Recovery src/QingLi.Package docs scripts
git commit -m "build: ship safe taskbar clock replacement"
```

## Completion Gate

替换模式只有在自动化测试全绿，并且“开启、失败回滚、手动关闭、异常恢复、卸载恢复”五条路径全部通过真实 Windows 11 手工验证后才可默认展示为可用功能。任何未通过的系统构建都应自动退回托盘模式。
