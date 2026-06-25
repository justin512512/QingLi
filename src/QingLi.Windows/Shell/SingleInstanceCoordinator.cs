using System.IO.Pipes;
using System.IO;
using System.Text;
using System.Threading.Channels;

namespace QingLi.Windows.Shell;

public sealed class SingleInstanceCoordinator(string applicationName) : IAsyncDisposable
{
    private readonly string _mutexName = $@"Local\{applicationName}.SingleInstance";
    private readonly string _pipeName = $"{applicationName}.Activation";
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Channel<string> _activations = Channel.CreateUnbounded<string>();
    private Mutex? _mutex;
    private Task? _listener;

    public event Action<string>? ActivationRequested;

    public Task<bool> TryAcquireAsync()
    {
        _mutex = new Mutex(false, _mutexName, out var createdNew);
        if (createdNew)
        {
            _listener = ListenAsync(_shutdown.Token);
        }

        return Task.FromResult(createdNew);
    }

    public Task<string> WaitForActivationAsync(CancellationToken cancellationToken = default) =>
        _activations.Reader.ReadAsync(cancellationToken).AsTask();

    public async Task SignalPrimaryAsync(
        string command,
        CancellationToken cancellationToken = default)
    {
        await using var pipe = new NamedPipeClientStream(
            ".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(2_000, cancellationToken);
        await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true
        };
        await writer.WriteLineAsync(command.AsMemory(), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync();
        if (_listener is not null)
        {
            try
            {
                await _listener;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _activations.Writer.TryComplete();
        _mutex?.Dispose();
        _shutdown.Dispose();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            await pipe.WaitForConnectionAsync(cancellationToken);
            using var reader = new StreamReader(pipe, Encoding.UTF8);
            var command = await reader.ReadLineAsync(cancellationToken);
            if (command is null)
            {
                continue;
            }

            ActivationRequested?.Invoke(command);
            await _activations.Writer.WriteAsync(command, cancellationToken);
        }
    }
}
