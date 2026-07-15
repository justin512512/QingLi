using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QingLi.Infrastructure.Updates;

public sealed class ValidatedDataPackageStore(string rootDirectory)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public async Task<string> InstallAsync(
        DataPackageManifest manifest,
        Stream packageContents,
        Func<string, CancellationToken, Task> validatePackage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(packageContents);
        ArgumentNullException.ThrowIfNull(validatePackage);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateManifest(manifest);

        var directory = GetPackageDirectory(manifest.PackageName);
        var currentManifest = TryReadCurrentManifest(directory);
        if (currentManifest is not null
            && string.Compare(manifest.Version, currentManifest.Version, StringComparison.Ordinal) <= 0)
        {
            throw new InvalidDataException("The data package version is not newer than the installed version.");
        }

        Directory.CreateDirectory(directory);
        var candidatePath = Path.Combine(directory, $".{Guid.NewGuid():N}.candidate");
        var pointerCandidate = Path.Combine(directory, $".{Guid.NewGuid():N}.manifest");
        try
        {
            await using (var target = new FileStream(
                candidatePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await packageContents.CopyToAsync(target, cancellationToken);
                await target.FlushAsync(cancellationToken);
            }

            var actualHash = await ComputeSha256Async(candidatePath, cancellationToken);
            if (!string.Equals(actualHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The data package SHA-256 does not match its manifest.");
            }

            await validatePackage(candidatePath, cancellationToken);
            var finalPath = Path.Combine(directory, $"{manifest.Version}.package");
            File.Move(candidatePath, finalPath, true);

            await File.WriteAllTextAsync(
                pointerCandidate,
                JsonSerializer.Serialize(manifest, JsonOptions),
                cancellationToken);
            File.Move(pointerCandidate, GetPointerPath(directory), true);
            return finalPath;
        }
        finally
        {
            TryDelete(candidatePath);
            TryDelete(pointerCandidate);
        }
    }

    public string ResolvePackagePath(string packageName, string bundledPath)
    {
        try
        {
            var directory = GetPackageDirectory(packageName);
            var manifest = TryReadCurrentManifest(directory);
            if (manifest is null || !string.Equals(manifest.PackageName, packageName, StringComparison.Ordinal))
            {
                return bundledPath;
            }

            var installedPath = Path.Combine(directory, $"{manifest.Version}.package");
            return File.Exists(installedPath) ? installedPath : bundledPath;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            return bundledPath;
        }
    }

    private string GetPackageDirectory(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName)
            || packageName.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new InvalidDataException("The data package name is invalid.");
        }

        return Path.Combine(rootDirectory, packageName);
    }

    private static void ValidateManifest(DataPackageManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Version)
            || manifest.Version.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '-' and not '_')
            || !Uri.TryCreate(manifest.DownloadUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || manifest.Sha256.Length != 64
            || manifest.Sha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException("The data package manifest is invalid.");
        }
    }

    private static DataPackageManifest? TryReadCurrentManifest(string directory)
    {
        var path = GetPointerPath(directory);
        if (!File.Exists(path)) return null;
        var manifest = JsonSerializer.Deserialize<DataPackageManifest>(File.ReadAllText(path), JsonOptions);
        if (manifest is null) throw new InvalidDataException("The installed data package pointer is empty.");
        ValidateManifest(manifest);
        return manifest;
    }

    private static string GetPointerPath(string directory) => Path.Combine(directory, "current.json");

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
