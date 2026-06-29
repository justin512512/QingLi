using System.Windows;
using PhysicalSize = System.Windows.Size;

namespace QingLi.Windows.ClockReplacement;

public static class ClockWindowPlacement
{
    public static Rect Calculate(TaskbarGeometry geometry, PhysicalSize physicalPixelSize)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        if (geometry.Edge != TaskbarEdge.Bottom)
        {
            throw new NotSupportedException(
                $"Taskbar edge '{geometry.Edge}' is not supported by the clock replacement.");
        }

        if (!IsFinitePositive(physicalPixelSize.Width) ||
            !IsFinitePositive(physicalPixelSize.Height))
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPixelSize),
                "Clock size must contain finite, positive physical-pixel dimensions.");
        }

        var bounds = geometry.Bounds;
        if (!IsFiniteRect(bounds) ||
            physicalPixelSize.Width > bounds.Width ||
            physicalPixelSize.Height > bounds.Height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPixelSize),
                "Clock size must fit inside the taskbar's physical-pixel bounds.");
        }

        return new Rect(
            bounds.Right - physicalPixelSize.Width,
            bounds.Top + ((bounds.Height - physicalPixelSize.Height) / 2d),
            physicalPixelSize.Width,
            physicalPixelSize.Height);
    }

    private static bool IsFinitePositive(double value) =>
        double.IsFinite(value) && value > 0;

    private static bool IsFiniteRect(Rect rect) =>
        !rect.IsEmpty &&
        double.IsFinite(rect.X) &&
        double.IsFinite(rect.Y) &&
        IsFinitePositive(rect.Width) &&
        IsFinitePositive(rect.Height);
}
