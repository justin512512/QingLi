using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace QingLi.Windows.Tray;

public static class QingLiTrayIcon
{
    public static Icon Create()
    {
        using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var blue = new SolidBrush(Color.FromArgb(55, 119, 232));
        using var white = new SolidBrush(Color.White);
        using var red = new SolidBrush(Color.FromArgb(255, 89, 104));
        using var gridPen = new Pen(Color.FromArgb(185, 205, 235), 1.4f);

        graphics.FillRoundedRectangle(blue, new RectangleF(1, 1, 30, 30), 7);
        graphics.FillRectangle(white, 5, 6, 22, 21);
        graphics.FillRectangle(red, 5, 6, 22, 6);
        graphics.DrawLine(gridPen, 9, 16, 23, 16);
        graphics.DrawLine(gridPen, 9, 21, 23, 21);
        graphics.DrawLine(gridPen, 13, 14, 13, 24);
        graphics.DrawLine(gridPen, 19, 14, 19, 24);

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static void FillRoundedRectangle(
        this Graphics graphics,
        Brush brush,
        RectangleF bounds,
        float radius)
    {
        using var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint handle);
}
