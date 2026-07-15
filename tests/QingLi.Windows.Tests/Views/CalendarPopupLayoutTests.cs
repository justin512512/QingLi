using System.Xml.Linq;

namespace QingLi.Windows.Tests.Views;

public sealed class CalendarPopupLayoutTests
{
    [Fact]
    public void PopupUsesApprovedThreeColumnInformationLayout()
    {
        var document = XDocument.Load(GetXamlPath());
        var window = document.Root!;

        Assert.Equal("1040", window.Attribute("Width")?.Value);
        Assert.Equal("520", window.Attribute("Height")?.Value);
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
    public void PopupAllowsNativeResizeWithinMinimumDimensions()
    {
        var window = XDocument.Load(GetXamlPath()).Root!;

        Assert.Equal("CanResizeWithGrip", window.Attribute("ResizeMode")?.Value);
        Assert.Equal("760", window.Attribute("MinWidth")?.Value);
        Assert.Equal("420", window.Attribute("MinHeight")?.Value);
    }

    [Fact]
    public void PopupExposesOnlyATopStripAsTheDragSurface()
    {
        var document = XDocument.Load(GetXamlPath());
        var dragHandle = FindNamed(document, "DragHandle");
        var dragHandleAffordance = FindNamed(document, "DragHandleAffordance");

        Assert.NotNull(dragHandle);
        Assert.Equal("Thumb", dragHandle!.Name.LocalName);
        Assert.Equal("28", dragHandle.Attribute("Height")?.Value);
        Assert.Equal("Top", dragHandle.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("SizeAll", dragHandle.Attribute("Cursor")?.Value);
        Assert.Equal("OnDragHandleMouseLeftButtonDown",
            dragHandle.Attributes().Single(attribute => attribute.Name.LocalName == "PreviewMouseLeftButtonDown").Value);
        Assert.False(string.IsNullOrWhiteSpace(dragHandle.Attribute("ToolTip")?.Value));
        Assert.False(string.IsNullOrWhiteSpace(
            dragHandle.Attribute("AutomationProperties.HelpText")?.Value));
        Assert.NotNull(dragHandleAffordance);
        Assert.Equal("False", dragHandleAffordance!.Attribute("IsHitTestVisible")?.Value);
    }

    [Fact]
    public void PopupUsesResponsiveTwoThreeTwoContentColumns()
    {
        var document = XDocument.Load(GetXamlPath());
        var contentGrid = FindNamed(document, "HistoryColumn")!.Parent!;
        var columnDefinitions = contentGrid.Elements()
            .Single(element => element.Name.LocalName == "Grid.ColumnDefinitions")
            .Elements()
            .Select(element => element.Attribute("Width")?.Value)
            .ToArray();

        Assert.Equal(new[] { "2*", "12", "3*", "12", "2*" }, columnDefinitions);
    }

    [Fact]
    public void PopupShowsANonInteractiveBottomRightResizeAffordance()
    {
        var document = XDocument.Load(GetXamlPath());
        var resizeGrip = FindNamed(document, "ResizeGrip");

        Assert.NotNull(resizeGrip);
        Assert.Equal("Bottom", resizeGrip!.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("Right", resizeGrip.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("False", resizeGrip.Attribute("IsHitTestVisible")?.Value);
    }

    [Fact]
    public void PopupAvoidsTinyTextThatIsHardToRead()
    {
        var document = XDocument.Load(GetXamlPath());
        var fontSizes = document.Descendants()
            .SelectMany(element => element.Attributes())
            .Where(attribute => attribute.Name.LocalName == "FontSize")
            .Select(attribute => double.Parse(attribute.Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();

        Assert.NotEmpty(fontSizes);
        Assert.All(fontSizes, size => Assert.True(size >= 11, $"Font size {size} is too small."));
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
