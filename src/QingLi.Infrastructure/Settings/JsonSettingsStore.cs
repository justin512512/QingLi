using System.Text.Json;
using System.Text.Json.Serialization;
using QingLi.Core.Settings;

namespace QingLi.Infrastructure.Settings;

public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _path = Path.GetFullPath(path);

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return AppSettings.Default;
        }

        try
        {
            await using var stream = new FileStream(
                _path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<AppSettings>(
                stream, JsonOptions, cancellationToken) ?? AppSettings.Default;
        }
        catch (JsonException)
        {
            await PreserveCorruptFileAsync(cancellationToken);
            return AppSettings.Default;
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
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
                    stream, settings, JsonOptions, cancellationToken);
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

    private async Task PreserveCorruptFileAsync(CancellationToken cancellationToken)
    {
        var copyPath = $"{_path}.{DateTime.UtcNow:yyyyMMddHHmmssfff}.corrupt-copy";
        await using var source = new FileStream(
            _path, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var target = new FileStream(
            copyPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(target, cancellationToken);
    }
}
