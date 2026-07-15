using QingLi.Windows.Views;

namespace QingLi.Windows.Tests.Views;

public sealed class WindowResizeHitTestTests
{
    [Theory]
    [InlineData(1, 250, (int)WindowResizeHitTarget.Left)]
    [InlineData(999, 250, (int)WindowResizeHitTarget.Right)]
    [InlineData(500, 1, (int)WindowResizeHitTarget.Top)]
    [InlineData(500, 499, (int)WindowResizeHitTarget.Bottom)]
    [InlineData(1, 1, (int)WindowResizeHitTarget.TopLeft)]
    [InlineData(999, 1, (int)WindowResizeHitTarget.TopRight)]
    [InlineData(1, 499, (int)WindowResizeHitTarget.BottomLeft)]
    [InlineData(999, 499, (int)WindowResizeHitTarget.BottomRight)]
    [InlineData(500, 250, (int)WindowResizeHitTarget.Client)]
    public void HitTestClassifiesEveryResizeDirectionAndClientInterior(
        int x,
        int y,
        int expected)
    {
        var actual = WindowResizeHitTest.Classify(
            x, y, width: 1000, height: 500, dpiScaleX: 1, dpiScaleY: 1);

        Assert.Equal((WindowResizeHitTarget)expected, actual);
    }

    [Fact]
    public void HitTestScalesTheResizeBorderFromDipsToPhysicalPixels()
    {
        Assert.Equal(WindowResizeHitTarget.Left,
            WindowResizeHitTest.Classify(
                x: 12, y: 250, width: 2000, height: 1000, dpiScaleX: 2, dpiScaleY: 2));
        Assert.Equal(WindowResizeHitTarget.Client,
            WindowResizeHitTest.Classify(
                x: 17, y: 250, width: 2000, height: 1000, dpiScaleX: 2, dpiScaleY: 2));
    }
}
