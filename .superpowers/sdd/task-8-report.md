# Task 8 Report — Tray entry and calendar popup

Status: needs-fixes-resolved

Current commit SHA: pending final commit

What changed in this repair round:

- Removed the `AppContext.BaseDirectory\QingLiData` fallback.
- Fixed database location to `%LOCALAPPDATA%\QingLi\qingli.db` through a testable helper.
- Added startup failure handling that shows a clear error and shuts the app down.
- Replaced synchronous month-navigation calls with a real async command implementation.
- Removed `ConfigureAwait(false)` from the calendar load path so collection updates stay on the caller context.

RED / GREEN:

- RED: added `AppPathsTests` and `CalendarPopupAsyncCommandTests`; they failed immediately because `AppPaths` and `ExecuteAsync` did not exist.
- RED: the delayed repository test exposed a queue-consumption bug in the test fixture.
- GREEN: implemented `AppPaths.GetDatabasePath(...)`, `AsyncCommand`, and the startup failure path; updated the async test fixture and the existing month-navigation test to await the async command.

Verification:

- `dotnet test tests/QingLi.Windows.Tests/QingLi.Windows.Tests.csproj --filter "FullyQualifiedName~AppPathsTests|FullyQualifiedName~CalendarPopupViewModelTests|FullyQualifiedName~CalendarPopupAsyncCommandTests|FullyQualifiedName~TrayIconServiceTests" -p:NuGetAudit=false`
- `dotnet test QingLi.sln -p:NuGetAudit=false`
- `dotnet run --project src/QingLi.Windows --no-build -p:NuGetAudit=false`

Results:

- Focused Windows tests passed.
- Full solution tests passed.
- The startup smoke run stayed resident as expected for a tray app and hit the harness timeout rather than crashing.

Notes:

- The tray menu still uses the exact required labels.
- The popup view model still delegates lunar/calendar math to `CalendarMonthService`.
- Birthday dates are merged from `BirthdayOccurrenceService`.
