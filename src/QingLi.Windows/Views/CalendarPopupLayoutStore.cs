using System.IO;
using System.Text.Json;

namespace QingLi.Windows.Views;

public sealed record CalendarPopupLayout(
    double Left,
    double Top,
    double Width,
    double Height,
    bool IsCustomized);

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
    private readonly SemaphoreSlim _mutationGate = new(1, 1);

    public JsonCalendarPopupLayoutStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _path = path;
        _tempPath = path + ".tmp";
    }

    public async Task<CalendarPopupLayout?> LoadAsync(CancellationToken cancellationToken)
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

    public async Task SaveAsync(CalendarPopupLayout layout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (!IsValid(layout))
        {
            throw new ArgumentOutOfRangeException(nameof(layout), "Popup layout values are invalid.");
        }

        await _mutationGate.WaitAsync(cancellationToken);
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
                    await JsonSerializer.SerializeAsync(stream, layout, cancellationToken: cancellationToken);
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
            _mutationGate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            File.Delete(_path);
            File.Delete(_tempPath);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    private static bool IsValid(CalendarPopupLayout layout) =>
        double.IsFinite(layout.Left) &&
        double.IsFinite(layout.Top) &&
        double.IsFinite(layout.Width) &&
        double.IsFinite(layout.Height) &&
        layout.Width >= MinimumWidth &&
        layout.Height >= MinimumHeight;
}
