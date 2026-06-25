using System.Windows.Input;

namespace QingLi.Windows.ViewModels;

public sealed class AsyncCommand : ICommand
{
    private readonly Func<Task> _executeAsync;
    private readonly object _gate = new();
    private Task? _runningTask;
    private bool _isExecuting;

    public AsyncCommand(Func<Task> executeAsync)
    {
        _executeAsync = executeAsync;
    }

    public event EventHandler? CanExecuteChanged;

    public event EventHandler<Exception>? ErrorOccurred;

    public Exception? LastError { get; private set; }

    public bool IsExecuting
    {
        get
        {
            lock (_gate)
            {
                return _isExecuting;
            }
        }
    }

    public bool CanExecute(object? parameter) => !IsExecuting;

    public Task ExecuteAsync()
    {
        lock (_gate)
        {
            if (_runningTask is { IsCompleted: false })
            {
                return _runningTask;
            }

            _isExecuting = true;
            OnCanExecuteChanged();
            _runningTask = ExecuteCoreAsync();
            return _runningTask;
        }
    }

    public void Execute(object? parameter)
    {
        _ = ExecuteAsync();
    }

    private async Task ExecuteCoreAsync()
    {
        try
        {
            LastError = null;
            await _executeAsync();
        }
        catch (Exception exception)
        {
            LastError = exception;
            ErrorOccurred?.Invoke(this, exception);
        }
        finally
        {
            lock (_gate)
            {
                _isExecuting = false;
                _runningTask = null;
            }

            OnCanExecuteChanged();
        }
    }

    private void OnCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
