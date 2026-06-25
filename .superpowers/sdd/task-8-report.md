# Task 8 Report — Tray entry and calendar popup

Status: done

Commit SHA: `b60286563d8c4640eb050635fa4ed4d71d24fbbd`

What changed:

- Added `CalendarPopupViewModel` with month navigation, `LoadMonthAsync`, and birthday merging via `BirthdayOccurrenceService`.
- Added a Windows 11 style popup window with Esc / deactivated close and work-area positioning.
- Added a WinForms `NotifyIcon` tray service with the exact right-click menu text from the brief.
- Removed the template `MainWindow` and removed `StartupUri` so the app starts from the tray only.
- Added a writable data-directory fallback for the sandboxed environment so startup can complete during smoke tests.

Verification:

- `dotnet test QingLi.sln -p:NuGetAudit=false`
- `dotnet run --project src/QingLi.Windows --no-build -p:NuGetAudit=false`

Results:

- Full test suite passed: 44 tests total.
- Smoke start no longer throws startup exceptions; the app keeps running as expected for a tray app until the harness timeout stops it.

Notes:

- The popup view model does not calculate lunar data itself; it uses `CalendarMonthService`.
- Birthday occurrence dates are merged from `BirthdayOccurrenceService`, and selected-day details show the matched birthday names.
- Tray menu text is exactly: 打开日历、添加生日、设置、暂停今日提醒、退出.
