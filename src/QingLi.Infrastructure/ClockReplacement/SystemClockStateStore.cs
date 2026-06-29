using System.Text.Json;
using System.Text.Json.Serialization;
using QingLi.Core.ClockReplacement;

namespace QingLi.Infrastructure.ClockReplacement;

public sealed class SystemClockStateStore : ISystemClockStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly string _path;

    public SystemClockStateStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QingLi",
            "system-clock-state.json"))
    {
    }

    public SystemClockStateStore(string path) => _path = Path.GetFullPath(path);

    public async Task<SystemClockState?> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            await using var stream = new FileStream(
                _path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var state = await JsonSerializer.DeserializeAsync<SystemClockState>(
                stream, JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("The system clock state file contains no state.");
            ValidateState(state);
            return state;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The system clock state file is invalid.", exception);
        }
    }

    public async Task SaveAsync(SystemClockState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateState(state);
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(
                    stream, state, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, _path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(_path);
        return Task.CompletedTask;
    }

    private static void ValidateState(SystemClockState state)
    {
        if (state.ValueExisted != state.OriginalValue.HasValue)
        {
            throw new InvalidDataException(
                "The system clock state does not consistently describe the original value.");
        }
    }
}
