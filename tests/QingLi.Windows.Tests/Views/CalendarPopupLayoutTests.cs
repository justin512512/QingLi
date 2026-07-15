using System.Xml.Linq;

namespace QingLi.Windows.Tests.Views;

public sealed class CalendarPopupLayoutTests
{
    [Fact]
    public void PopupUsesApprovedThreeColumnInformationLayout()
    {
        var document = XDocument.Load(GetXamlPath());
        var window = document.Root!;

        Assert.Equal("900", window.Attribute("Width")?.Value);
        Assert.Equal("440", window.Attribute("Height")?.Value);
        Assert.Equal("False", window.Attribute("ShowInTaskbar")?.Value);
        Assert.Equal("None", window.Attribute("WindowStyle")?.Value);

        Assert.NotNull(FindNamed(document, "HistoryColumn"));
        Assert.NotNull(FindNamed(document, "CalendarColumn"));
        Assert.NotNull(FindNamed(document, "UpcomingColumn"));
        Assert.NotNull(FindNamed(document, "HistoryScrollViewer"));
        Assert.NotNull(FindNamed(document, "UpcomingScrollViewer"));
        Assert.NotNull(FindNamed(document, "CalendarDaysList"));
        Assert.NotNull(FindNamed(document, "AddBirthdayButton"));
        Assert.NotNull(FindNamed(document, "AddAnniversaryButton"));
        Assert.NotNull(FindNamed(document, "SettingsButton"));
    }

    [Fact]
    public void PopupContainsAllRequiredVisibleInformation()
    {
        var text = File.ReadAllText(GetXamlPath());

        Assert.Contains("历史上的今天", text);
        Assert.Contains("最近节日节气", text);
        Assert.Contains("Almanac.GanZhiText", text);
        Assert.Contains("Almanac.ZodiacText", text);
        Assert.Contains("Almanac.Suitable", text);
        Assert.Contains("Almanac.Avoid", text);
        Assert.Contains("Calendar.WeekdayHeaders", text);
        Assert.Contains("Calendar.Days", text);
        Assert.Contains("HistoryToday", text);
        Assert.Contains("UpcomingEvents", text);
        Assert.Contains("休", text);
        Assert.Contains("班", text);
    }

    private static XElement? FindNamed(XDocument document, string name) =>
        document.Descendants().FirstOrDefault(element =>
            element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == name));

    private static string GetXamlPath() => Path.Combine(
        GetRepositoryRoot(), "src", "QingLi.Windows", "Views", "CalendarPopupWindow.xaml");

    private static string GetRepositoryRoot() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
