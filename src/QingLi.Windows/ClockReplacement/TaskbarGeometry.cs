using System.Windows;

namespace QingLi.Windows.ClockReplacement;

public enum TaskbarEdge
{
    Left = 0,
    Top = 1,
    Right = 2,
    Bottom = 3
}

public sealed record TaskbarGeometry(Rect Bounds, TaskbarEdge Edge, double DpiScale);

public readonly record struct TaskbarNativeData(
    Rect Bounds,
    TaskbarEdge Edge,
    Rect MonitorBounds,
    uint Dpi);
