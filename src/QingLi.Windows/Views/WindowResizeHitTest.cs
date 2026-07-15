namespace QingLi.Windows.Views;

internal enum WindowResizeHitTarget
{
    Client = 1,
    Left = 10,
    Right = 11,
    Top = 12,
    TopLeft = 13,
    TopRight = 14,
    Bottom = 15,
    BottomLeft = 16,
    BottomRight = 17
}

internal static class WindowResizeHitTest
{
    private const double ResizeBorderThickness = 8;

    internal static WindowResizeHitTarget Classify(
        int x,
        int y,
        int width,
        int height,
        double dpiScaleX,
        double dpiScaleY)
    {
        var horizontalBorder = Math.Max(1, (int)Math.Ceiling(ResizeBorderThickness * dpiScaleX));
        var verticalBorder = Math.Max(1, (int)Math.Ceiling(ResizeBorderThickness * dpiScaleY));
        var left = x >= 0 && x < horizontalBorder;
        var right = x < width && x >= width - horizontalBorder;
        var top = y >= 0 && y < verticalBorder;
        var bottom = y < height && y >= height - verticalBorder;

        if (top && left) return WindowResizeHitTarget.TopLeft;
        if (top && right) return WindowResizeHitTarget.TopRight;
        if (bottom && left) return WindowResizeHitTarget.BottomLeft;
        if (bottom && right) return WindowResizeHitTarget.BottomRight;
        if (left) return WindowResizeHitTarget.Left;
        if (right) return WindowResizeHitTarget.Right;
        if (top) return WindowResizeHitTarget.Top;
        if (bottom) return WindowResizeHitTarget.Bottom;
        return WindowResizeHitTarget.Client;
    }
}
