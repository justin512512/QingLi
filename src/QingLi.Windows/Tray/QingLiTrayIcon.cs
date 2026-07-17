using System.Drawing;
using System.IO;

namespace QingLi.Windows.Tray;

public static class QingLiTrayIcon
{
    private static readonly Uri ResourceUri = new(
        "/QingLi.Windows;component/Assets/Brand/QingLi.ico",
        UriKind.Relative);

    public static Icon Create() => Create(OpenResourceStream);

    internal static Icon Create(Func<Stream> openResourceStream)
    {
        using var stream = openResourceStream();
        using var icon = new Icon(stream, 32, 32);
        return (Icon)icon.Clone();
    }

    private static Stream OpenResourceStream() =>
        System.Windows.Application.GetResourceStream(ResourceUri)?.Stream
        ?? throw new InvalidOperationException($"WPF resource '{ResourceUri}' was not found.");
}
