# Task 9 Report

## Summary

- Added birthday editor, birthday manager, and settings view models with async commands and test coverage.
- Added birthday manager, birthday editor, and settings windows with simple Windows 11 style card layouts.
- Wired tray menu actions so “添加生日” opens the birthday manager window and “设置” opens the settings window.
- Added fixed app data path support through `AppPaths.DataDirectory` and used local settings plus startup registration in the settings flow.

## TDD Notes

- Wrote failing tests first for birthday validation, reminder day range, reminder time parsing, lunar validation, save-without-write-on-invalid, async birthday loading/search/delete, settings persistence, high-contrast behavior, fixed data directory opening, disabled replace-clock state, and singleton window hosting.
- Verified the initial red state from missing types.
- Implemented the minimum production code to satisfy those tests, then reran the focused Windows test groups and the full suite.

## Verification

- `E:\claude_data\.dotnet\dotnet.exe test E:\claude_data\projects\QingLi\.worktrees\qingli-desktop\QingLi.sln -p:NuGetAudit=false`
  - Passed: 73
  - Failed: 0
- Short startup check
  - Launched `src\QingLi.Windows\bin\Debug\net8.0-windows\QingLi.Windows.exe`
  - Process stayed alive for 5 seconds and was then stopped intentionally

## Key Implementation Details

- `BirthdayEditorViewModel`
  - Validates name, month, Gregorian day-by-year, lunar day range, configurable reminder days `0..365`, reminder time parsing, and invalid lunar dates.
  - Does not call the repository when validation fails.
- `BirthdayManagerViewModel`
  - Loads birthdays asynchronously, applies name filter, sorts by next occurrence date, and deletes selected items asynchronously.
- `BirthdayManagerWindow`
  - Performs delete confirmation in the window before invoking the view model delete command.
- `SettingsViewModel`
  - Loads and saves all supported settings through `AppSettings` and `ISettingsStore`.
  - Calls `StartupTaskService` through `IStartupTaskService`.
  - Uses `AppPaths.DataDirectory` for opening the data directory.
  - Exposes high-contrast state to disable custom color editing and exposes disabled replace-clock messaging for this phase.
- `App.xaml.cs`
  - Loads settings from the fixed app data directory.
  - Uses the saved first-day-of-week setting when building the calendar popup.
  - Wires tray actions to real birthday manager and settings windows.

## Follow-up Notes

- The “暂停今日提醒” tray action remains unchanged in this task.
- Full test runs in this environment require `-p:NuGetAudit=false` because offline NuGet vulnerability auditing raises `NU1900` against the default `https://api.nuget.org/v3/index.json` endpoint.
