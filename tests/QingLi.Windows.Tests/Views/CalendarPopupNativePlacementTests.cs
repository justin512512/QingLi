using System.Windows;
using QingLi.Windows.Views;

namespace QingLi.Windows.Tests.Views;

public sealed class CalendarPopupNativePlacementTests
{
    [Fact]
    public void ApplyPassesRoundedPhysicalBoundsIncludingNegativeOrigins()
    {
        (nint Handle, int X, int Y, int Width, int Height)? actual = null;

        CalendarPopupNativePlacement.Apply(
            (nint)42,
            new Rect(-2400.4, -100.6, 1560.2, 780.4),
            (handle, x, y, width, height) =>
            {
                actual = (handle, x, y, width, height);
                return true;
            });

        Assert.Equal(((nint)42, -2400, -101, 1560, 780), actual);
    }

    [Fact]
    public void ApplyReportsNativePlacementFailure()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CalendarPopupNativePlacement.Apply(
                (nint)42,
                new Rect(1920, 0, 1560, 780),
                (_, _, _, _, _) => false));
    }

    [Fact]
    public void TryApplyWithFallbackContainsFailureAndRetainsSafeBounds()
    {
        var calls = new List<(int X, int Y, int Width, int Height)>();
        var failure = default(Exception);

        var applied = CalendarPopupNativePlacement.TryApplyWithFallback(
            (nint)42,
            new Rect(1920, 0, 1560, 780),
            new Rect(100, 120, 1040, 520),
            exception => failure = exception,
            (_, x, y, width, height) =>
            {
                calls.Add((x, y, width, height));
                return calls.Count == 2;
            });

        Assert.True(applied);
        Assert.Equal(
            [(1920, 0, 1560, 780), (100, 120, 1040, 520)],
            calls);
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public void TryApplyWithFallbackNeverThrowsWhenNativeCallsAndReporterFail()
    {
        var callCount = 0;
        var exception = Record.Exception(() =>
            CalendarPopupNativePlacement.TryApplyWithFallback(
                (nint)42,
                new Rect(1920, 0, 1560, 780),
                new Rect(100, 120, 1040, 520),
                _ => throw new InvalidOperationException("report failed"),
                (_, _, _, _, _) =>
                {
                    callCount++;
                    throw new System.Runtime.InteropServices.ExternalException(
                        "native positioner failed");
                }));

        Assert.Null(exception);
        Assert.Equal(2, callCount);
    }
}
