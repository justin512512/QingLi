using System.Windows;

namespace QingLi.Windows.Shell;

public interface IAppWindow
{
    event EventHandler? Closed;

    bool IsVisible { get; }

    void Show();

    void Activate();
}

public sealed class SingletonWindowHost(Func<IAppWindow> factory)
{
    private IAppWindow? _window;

    public void Show()
    {
        if (_window is null)
        {
            _window = factory();
            _window.Closed += HandleClosed;
        }

        if (!_window.IsVisible)
        {
            _window.Show();
        }

        _window.Activate();
    }

    private void HandleClosed(object? sender, EventArgs e)
    {
        if (_window is null)
        {
            return;
        }

        _window.Closed -= HandleClosed;
        _window = null;
    }
}

public sealed class WindowAdapter(Window window) : IAppWindow
{
    public event EventHandler? Closed
    {
        add => window.Closed += value;
        remove => window.Closed -= value;
    }

    public bool IsVisible => window.IsVisible;

    public void Show() => window.Show();

    public void Activate() => window.Activate();
}
