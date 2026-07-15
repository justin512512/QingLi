using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

[assembly: InternalsVisibleTo("QingLi.Windows.Tests")]

namespace QingLi.Windows.Views;

public sealed record CalendarPopupLayout(
    double Left,
    double Top,
    double Width,
    double Height,
    bool IsCustomized,
    string? MonitorDeviceName = null);

public interface ICalendarPopupLayoutStore
{
    Task<CalendarPopupLayout?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(CalendarPopupLayout layout, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}

public sealed class JsonCalendarPopupLayoutStore : ICalendarPopupLayoutStore
{
    private const double MinimumWidth = 760;
    private const double MinimumHeight = 420;

    private readonly string _path;
    private readonly string _tempPath;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly Func<Stream, CalendarPopupLayout, CancellationToken, Task> _writeAsync;

    public JsonCalendarPopupLayoutStore(string path)
        : this(
            path,
            static (stream, layout, cancellationToken) =>
                JsonSerializer.SerializeAsync(stream, layout, cancellationToken: cancellationToken))
    {
    }

    internal JsonCalendarPopupLayoutStore(
        string path,
        Func<Stream, CalendarPopupLayout, CancellationToken, Task> writeAsync)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(writeAsync);

        _path = path;
        _tempPath = path + ".tmp";
        _writeAsync = writeAsync;
    }

    public async Task<CalendarPopupLayout?> LoadAsync(CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            try
            {
                await using var stream = new FileStream(
                    _path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    FileOptions.Asynchronous);
                var layout = await JsonSerializer.DeserializeAsync<CalendarPopupLayout>(
                    stream,
                    cancellationToken: cancellationToken);

                return layout is not null && IsValid(layout) ? layout : null;
            }
            catch (JsonException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task SaveAsync(CalendarPopupLayout layout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (!IsValid(layout))
        {
            throw new ArgumentOutOfRangeException(nameof(layout), "Popup layout values are invalid.");
        }

        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            try
            {
                await using (var stream = new FileStream(
                    _tempPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    FileOptions.Asynchronous))
                {
                    await _writeAsync(stream, layout, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                    stream.Flush(flushToDisk: true);
                }

                File.Move(_tempPath, _path, overwrite: true);
            }
            finally
            {
                File.Delete(_tempPath);
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            File.Delete(_path);
            File.Delete(_tempPath);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private static bool IsValid(CalendarPopupLayout layout) =>
        double.IsFinite(layout.Left) &&
        double.IsFinite(layout.Top) &&
        double.IsFinite(layout.Width) &&
        double.IsFinite(layout.Height) &&
        layout.Width >= MinimumWidth &&
        layout.Height >= MinimumHeight &&
        (layout.MonitorDeviceName is null || !string.IsNullOrWhiteSpace(layout.MonitorDeviceName));
}
