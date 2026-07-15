namespace QingLi.Windows.Tests;

public sealed class AppCompositionTests
{
    [Fact]
    public void AppComposesDashboardAndWiresQuickActions()
    {
        var source = File.ReadAllText(GetAppSourcePath());

        Assert.Contains("CalendarDashboardViewModel", source);
        Assert.Contains("JsonHistoryTodayProvider.LoadAsync", source);
        Assert.Contains("new UpcomingEventService", source);
        Assert.Contains("new CalendarPopupWindow(_calendarDashboardViewModel)", source);
        Assert.Contains("AddBirthdayRequested +=", source);
        Assert.Contains("AddAnniversaryRequested +=", source);
        Assert.Contains("SettingsRequested +=", source);
    }

    private static string GetAppSourcePath() => Path.Combine(
        GetRepositoryRoot(), "src", "QingLi.Windows", "App.xaml.cs");

    private static string GetRepositoryRoot() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
