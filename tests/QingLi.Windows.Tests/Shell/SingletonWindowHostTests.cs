using QingLi.Windows.Shell;

namespace QingLi.Windows.Tests.Shell;

public sealed class SingletonWindowHostTests
{
    [Fact]
    public void Show_creates_window_once_and_reuses_it()
    {
        var window = new FakeWindow();
        var createCalls = 0;
        var host = new SingletonWindowHost(() =>
        {
            createCalls++;
            return window;
        });

        host.Show();
        host.Show();

        Assert.Equal(1, createCalls);
        Assert.Equal(1, window.ShowCalls);
        Assert.Equal(2, window.ActivateCalls);
    }

    [Fact]
    public void Closed_window_is_recreated_on_next_show()
    {
        var first = new FakeWindow();
        var second = new FakeWindow();
        var windows = new Queue<FakeWindow>([first, second]);
        var host = new SingletonWindowHost(() => windows.Dequeue());

        host.Show();
        first.RaiseClosed();
        host.Show();

        Assert.Equal(1, first.ShowCalls);
        Assert.Equal(1, second.ShowCalls);
    }

    private sealed class FakeWindow : IAppWindow
    {
        public event EventHandler? Closed;

        public bool IsVisible { get; private set; }

        public int ShowCalls { get; private set; }

        public int ActivateCalls { get; private set; }

        public void Show()
        {
            ShowCalls++;
            IsVisible = true;
        }

        public void Activate() => ActivateCalls++;

        public void RaiseClosed()
        {
            IsVisible = false;
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}
