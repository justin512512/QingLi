using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Markup;
using System.Xml.Linq;

namespace QingLi.Windows.Tests.Views;

public sealed class CalendarPopupAutomationTests
{
    [Fact]
    public void DragHandleCreatesPeerWithAccessibleNameAndHelpText()
    {
        RunInSta(() =>
        {
            var dragHandle = LoadDragHandle();
            dragHandle.ApplyTemplate();

            var peer = UIElementAutomationPeer.CreatePeerForElement(dragHandle);

            Assert.NotNull(peer);
            Assert.Equal("Window drag handle", peer!.GetName());
            Assert.Equal("Drag to move the calendar window", peer.GetHelpText());
        });
    }

    private static FrameworkElement LoadDragHandle()
    {
        var document = XDocument.Load(GetXamlPath());
        var fragment = new XElement(document.Descendants().Single(element =>
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "DragHandle")));

        fragment.DescendantsAndSelf().Attributes()
            .Where(attribute => attribute.Name.LocalName == "Name" ||
                                attribute.Name.LocalName.EndsWith("MouseLeftButtonDown", StringComparison.Ordinal))
            .Remove();

        return Assert.IsAssignableFrom<FrameworkElement>(
            XamlReader.Parse(fragment.ToString(SaveOptions.DisableFormatting)));
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static string GetXamlPath() => Path.Combine(
        GetRepositoryRoot(), "src", "QingLi.Windows", "Views", "CalendarPopupWindow.xaml");

    private static string GetRepositoryRoot() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
