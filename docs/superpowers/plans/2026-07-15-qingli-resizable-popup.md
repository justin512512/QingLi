# QingLi Resizable Calendar Popup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the borderless calendar popup freely movable and resizable, persist its layout safely, keep it visible across monitor changes, and provide a reset action in Settings.

**Architecture:** Add a focused JSON layout store and a pure layout constraint service, then let `CalendarPopupWindow` coordinate drag/resize events without parsing storage data itself. `App` owns the shared store and injects a reset callback into Settings so layout state has one source of truth.

**Tech Stack:** .NET 8, WPF, Windows Forms screen enumeration, System.Text.Json, xUnit, PowerShell release packaging.

---

## File map

- Create `src/QingLi.Windows/Views/CalendarPopupLayoutStore.cs`: layout record, store interface, atomic JSON implementation.
- Create `tests/QingLi.Windows.Tests/Views/CalendarPopupLayoutStoreTests.cs`: persistence, corruption, validation, and clear behavior.
- Modify `src/QingLi.Windows/Views/CalendarPopupPlacement.cs`: constrain restored rectangles to current work areas.
- Modify `tests/QingLi.Windows.Tests/Views/CalendarPopupPlacementTests.cs`: primary, secondary, removed-monitor, and oversized-layout cases.
- Create `src/QingLi.Windows/Views/CalendarPopupLayoutSession.cs`: restore/save/reset coordination and non-blocking persistence status.
- Modify `src/QingLi.Windows/Views/CalendarPopupWindow.xaml`: resize mode, minimum size, drag surface, resize grip.
- Modify `src/QingLi.Windows/Views/CalendarPopupWindow.xaml.cs`: restore, drag, save-on-completion, reset, and default placement.
- Modify `tests/QingLi.Windows.Tests/Views/CalendarPopupLayoutTests.cs`: XAML interaction contract.
- Modify `src/QingLi.Windows/ViewModels/SettingsViewModel.cs`: reset-layout command.
- Modify `src/QingLi.Windows/Views/SettingsWindow.xaml`: reset button and status/error display.
- Modify `src/QingLi.Windows/App.xaml.cs`: shared store construction and dependency wiring.
- Modify `tests/QingLi.Windows.Tests/ViewModels/SettingsViewModelTests.cs`: reset success and failure.
- Modify `docs/user-guide.md` and `docs/test-matrix-information-calendar.md`: user behavior and final verification.

### Task 1: Persisted popup layout store

**Files:**
- Create: `src/QingLi.Windows/Views/CalendarPopupLayoutStore.cs`
- Create: `tests/QingLi.Windows.Tests/Views/CalendarPopupLayoutStoreTests.cs`

- [ ] **Step 1: Write failing store tests**

Cover round-trip, missing file, corrupt JSON, invalid non-finite/small size, atomic replacement, and clear:

```csharp
[Fact]
public async Task Save_then_load_round_trips_custom_layout()
{
    var store = new JsonCalendarPopupLayoutStore(_path);
    var expected = new CalendarPopupLayout(120, 80, 980, 560, true);
    await store.SaveAsync(expected, default);
    Assert.Equal(expected, await store.LoadAsync(default));
}

[Theory]
[InlineData(double.NaN, 80, 980, 560)]
[InlineData(120, 80, 759, 560)]
[InlineData(120, 80, 980, 419)]
public async Task Invalid_layout_is_ignored(double left, double top, double width, double height)
{
    await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(
        new CalendarPopupLayout(left, top, width, height, true)));
    Assert.Null(await new JsonCalendarPopupLayoutStore(_path).LoadAsync(default));
}
```

- [ ] **Step 2: Run tests and verify RED**

Run: `E:\claude_data\.dotnet\dotnet.exe test tests\QingLi.Windows.Tests\QingLi.Windows.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~CalendarPopupLayoutStoreTests`

Expected: compilation fails because `JsonCalendarPopupLayoutStore` and `CalendarPopupLayout` do not exist.

- [ ] **Step 3: Implement the store**

Use these public contracts:

```csharp
public sealed record CalendarPopupLayout(
    double Left, double Top, double Width, double Height, bool IsCustomized);

public interface ICalendarPopupLayoutStore
{
    Task<CalendarPopupLayout?> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(CalendarPopupLayout layout, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}
```

Validate finite coordinates, width `>= 760`, height `>= 420`; write `<path>.tmp`, flush, then `File.Move(temp, path, true)`. Catch `JsonException`, `IOException`, and `UnauthorizedAccessException` during load and return `null`. `ClearAsync` deletes only the configured layout file and stale temp file.

- [ ] **Step 4: Verify GREEN**

Run the Task 1 command. Expected: all `CalendarPopupLayoutStoreTests` pass.

- [ ] **Step 5: Commit**

```text
git add src/QingLi.Windows/Views/CalendarPopupLayoutStore.cs tests/QingLi.Windows.Tests/Views/CalendarPopupLayoutStoreTests.cs
git commit -m "feat: persist calendar popup layout"
```

### Task 2: Safe multi-monitor layout restoration

**Files:**
- Modify: `src/QingLi.Windows/Views/CalendarPopupPlacement.cs`
- Modify: `tests/QingLi.Windows.Tests/Views/CalendarPopupPlacementTests.cs`

- [ ] **Step 1: Write failing constraint tests**

```csharp
[Fact]
public void Restored_layout_on_removed_monitor_moves_to_fallback_work_area()
{
    var saved = new Rect(2500, 100, 1000, 600);
    var fallback = new Rect(0, 0, 1920, 1032);
    var actual = CalendarPopupPlacement.ConstrainSaved(
        saved, [fallback], fallback, new Size(760, 420), 32);
    Assert.True(fallback.Contains(actual.TopLeft));
    Assert.True(actual.Right <= fallback.Right);
    Assert.True(actual.Bottom <= fallback.Bottom);
}

[Fact]
public void Oversized_layout_shrinks_to_work_area_without_breaking_minimum()
{
    var actual = CalendarPopupPlacement.ConstrainSaved(
        new Rect(10, 10, 3000, 2000),
        [new Rect(0, 0, 1280, 720)], new Rect(0, 0, 1280, 720),
        new Size(760, 420), 32);
    Assert.Equal(new Size(1280, 720), actual.Size);
}
```

- [ ] **Step 2: Run tests and verify RED**

Run: `E:\claude_data\.dotnet\dotnet.exe test tests\QingLi.Windows.Tests\QingLi.Windows.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~CalendarPopupPlacementTests`

Expected: FAIL because `ConstrainSaved` is missing.

- [ ] **Step 3: Implement `ConstrainSaved`**

Select the work area with the largest rectangle intersection. If every intersection is empty, use `fallbackWorkArea`. Clamp width and height to `[minimum, workArea size]`; clamp left/top so the full window is visible when possible. Preserve at least `visibleDragHeight` at the top when work-area geometry is unusually small.

- [ ] **Step 4: Verify GREEN and existing placement behavior**

Run the Task 2 command. Expected: all old and new placement tests pass.

- [ ] **Step 5: Commit**

```text
git add src/QingLi.Windows/Views/CalendarPopupPlacement.cs tests/QingLi.Windows.Tests/Views/CalendarPopupPlacementTests.cs
git commit -m "feat: constrain saved popup layout"
```

### Task 3: Borderless drag and resize UI

**Files:**
- Modify: `src/QingLi.Windows/Views/CalendarPopupWindow.xaml`
- Modify: `src/QingLi.Windows/Views/CalendarPopupWindow.xaml.cs`
- Modify: `tests/QingLi.Windows.Tests/Views/CalendarPopupLayoutTests.cs`

- [ ] **Step 1: Write failing XAML contract tests**

Assert the root window has `ResizeMode="CanResizeWithGrip"`, `MinWidth="760"`, `MinHeight="420"`; assert named `DragHandle` and `ResizeGrip` elements exist and the drag handle is wired to `OnDragHandleMouseLeftButtonDown`.

- [ ] **Step 2: Run tests and verify RED**

Run: `E:\claude_data\.dotnet\dotnet.exe test tests\QingLi.Windows.Tests\QingLi.Windows.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~CalendarPopupLayoutTests`

Expected: FAIL because the popup remains `ResizeMode="NoResize"` and lacks the controls.

- [ ] **Step 3: Implement the XAML interaction surface**

Set:

```xml
ResizeMode="CanResizeWithGrip"
MinWidth="760" MinHeight="420"
LocationChanged="OnWindowLayoutChanged"
SizeChanged="OnWindowLayoutChanged"
```

Add a transparent 28-DIP top overlay named `DragHandle` with `Cursor="SizeAll"`, and a bottom-right visual named `ResizeGrip` with `IsHitTestVisible="False"`. `ResizeMode="CanResizeWithGrip"` supplies the native edge/corner resizing; the visual only makes the affordance discoverable. Keep the drag handle above decorative borders but do not cover footer buttons.

- [ ] **Step 4: Implement dragging**

```csharp
private void OnDragHandleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (e.ChangedButton == MouseButton.Left)
    {
        DragMove();
        e.Handled = true;
    }
}
```

- [ ] **Step 5: Verify GREEN**

Run the Task 3 command. Expected: all popup layout tests pass.

- [ ] **Step 6: Commit**

```text
git add src/QingLi.Windows/Views/CalendarPopupWindow.xaml src/QingLi.Windows/Views/CalendarPopupWindow.xaml.cs tests/QingLi.Windows.Tests/Views/CalendarPopupLayoutTests.cs
git commit -m "feat: make calendar popup movable and resizable"
```

### Task 4: Restore and save user layout

**Files:**
- Modify: `src/QingLi.Windows/Views/CalendarPopupWindow.xaml.cs`
- Modify: `src/QingLi.Windows/App.xaml.cs`
- Create: `tests/QingLi.Windows.Tests/Views/CalendarPopupLayoutSessionTests.cs`

- [ ] **Step 1: Write failing session tests**

Extract a `CalendarPopupLayoutSession` that decides default vs saved placement and debounces layout completion. Test: saved custom layout wins; missing layout uses taskbar placement; programmatic initial positioning does not save; a user move/resize saves one final layout after a 300 ms idle interval; reset clears customization.

- [ ] **Step 2: Run tests and verify RED**

Run: `E:\claude_data\.dotnet\dotnet.exe test tests\QingLi.Windows.Tests\QingLi.Windows.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~CalendarPopupLayoutSessionTests`

Expected: compilation fails because the session type is missing.

- [ ] **Step 3: Implement session coordination**

Constructor inputs: `ICalendarPopupLayoutStore`, `Func<IReadOnlyList<Rect>> workAreas`, `Func<Rect> fallbackWorkArea`. Expose `RestoreAsync(defaultPlacement, cancellationToken)`, `MarkProgrammaticLayoutComplete()`, `ScheduleSave(Rect currentBounds)`, and `ResetAsync()`. Use a replaceable `CancellationTokenSource` plus 300 ms delay so only the final user layout is persisted. Catch background save failures, keep the popup usable, and publish the latest message through a `PersistenceFailed` event plus `LastPersistenceError` property.

- [ ] **Step 4: Wire `CalendarPopupWindow`**

Inject the store/session in the constructor. On first `Loaded`, await saved layout; apply constrained bounds when customized, otherwise call `PositionNearClickedTaskbar`. On `LocationChanged` and `SizeChanged`, schedule save only after initial restoration. Add `ResetLayoutAsync()` to clear, restore width/height `1040 × 520`, and mark next show for automatic placement.

- [ ] **Step 5: Wire `App`**

Create one `JsonCalendarPopupLayoutStore(Path.Combine(AppPaths.DataDirectory, "calendar-window-layout.json"))` and pass it to the popup plus Settings callback.

- [ ] **Step 6: Verify GREEN**

Run Task 4 tests and all `Views` tests. Expected: all pass.

- [ ] **Step 7: Commit**

```text
git add src/QingLi.Windows/App.xaml.cs src/QingLi.Windows/Views/CalendarPopupWindow.xaml.cs src/QingLi.Windows/Views/CalendarPopupLayoutSession.cs tests/QingLi.Windows.Tests/Views/CalendarPopupLayoutSessionTests.cs
git commit -m "feat: restore calendar popup layout"
```

### Task 5: Settings reset action

**Files:**
- Modify: `src/QingLi.Windows/ViewModels/SettingsViewModel.cs`
- Modify: `src/QingLi.Windows/Views/SettingsWindow.xaml`
- Modify: `src/QingLi.Windows/App.xaml.cs`
- Modify: `tests/QingLi.Windows.Tests/ViewModels/SettingsViewModelTests.cs`

- [ ] **Step 1: Write failing ViewModel tests**

```csharp
[Fact]
public async Task Reset_calendar_layout_invokes_callback_and_reports_success()
{
    var resets = 0;
    var vm = CreateViewModel(resetCalendarLayout: () => { resets++; return Task.CompletedTask; });
    await vm.ResetCalendarLayoutCommand.ExecuteAsync();
    Assert.Equal(1, resets);
    Assert.Equal("日历窗口布局已恢复默认", vm.LayoutResetMessage);
}
```

Add a failure test that exposes the callback exception in `LayoutResetMessage` without closing Settings.

- [ ] **Step 2: Run tests and verify RED**

Run: `E:\claude_data\.dotnet\dotnet.exe test tests\QingLi.Windows.Tests\QingLi.Windows.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~SettingsViewModelTests`

Expected: compilation fails because reset command/message are missing.

- [ ] **Step 3: Implement command and UI**

Add optional `Func<Task> resetCalendarLayout`, optional `Func<string?> layoutPersistenceErrorProvider`, `AsyncCommand ResetCalendarLayoutCommand`, and bind a Settings button labeled `恢复默认窗口布局`. Show `LayoutResetMessage` below it; when Settings loads, include any latest non-blocking background-save error from the provider. Wire the callback to `CalendarPopupWindow.ResetLayoutAsync()`; if the popup has not been created, clear the shared store directly.

- [ ] **Step 4: Verify GREEN**

Run Task 5 tests. Expected: all Settings tests pass.

- [ ] **Step 5: Commit**

```text
git add src/QingLi.Windows/App.xaml.cs src/QingLi.Windows/ViewModels/SettingsViewModel.cs src/QingLi.Windows/Views/SettingsWindow.xaml tests/QingLi.Windows.Tests/ViewModels/SettingsViewModelTests.cs
git commit -m "feat: reset calendar window layout"
```

### Task 6: Full verification and v4 package

**Files:**
- Modify: `docs/user-guide.md`
- Modify: `docs/test-matrix-information-calendar.md`

- [ ] **Step 1: Update documentation**

Document top-edge dragging, edge/corner resizing, saved layout path, screen recovery, minimum size, and Settings reset action. Record final test totals only after running them.

- [ ] **Step 2: Run full tests**

Run: `E:\claude_data\.dotnet\dotnet.exe test QingLi.sln -c Release --no-restore`

Expected: zero failures in Core, Infrastructure, and Windows suites.

- [ ] **Step 3: Build verified portable package**

Run: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\package.ps1 -Configuration Release -Runtime win-x64 -DotNetPath E:\claude_data\.dotnet\dotnet.exe -PortableOnly`

Expected: tests pass, publish succeeds, and post-package extraction/hash verification succeeds.

- [ ] **Step 4: Create and independently verify v4 artifact**

Copy the verified portable ZIP to `artifacts/QingLi-0.1.0-win-x64-portable-v4.zip`, run `python -m zipfile -t` against it, and record SHA-256.

- [ ] **Step 5: Commit release documentation**

```text
git add docs/user-guide.md docs/test-matrix-information-calendar.md
git commit -m "docs: prepare resizable popup release"
```
