using System.IO;
using System.Net.Http;
using QingLi.Infrastructure.Updates;

namespace QingLi.Windows.Services;

public sealed class HttpDataPackageDownloader(HttpClient httpClient) : IDataPackageDownloader
{
    public async Task DownloadAsync(
        DataPackageManifest manifest,
        Stream destination,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(manifest.DownloadUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException("Data package downloads require HTTPS.");
        }

        using var response = await httpClient.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await response.Content.CopyToAsync(destination, cancellationToken);
    }
}
