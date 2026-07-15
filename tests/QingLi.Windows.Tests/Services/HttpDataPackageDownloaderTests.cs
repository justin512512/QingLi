using System.Net;
using System.Text;
using QingLi.Infrastructure.Updates;
using QingLi.Windows.Services;

namespace QingLi.Windows.Tests.Services;

public sealed class HttpDataPackageDownloaderTests
{
    [Fact]
    public async Task DownloadsHttpsPackageIntoProvidedStream()
    {
        var client = new HttpClient(new Handler(HttpStatusCode.OK, "package"));
        var downloader = new HttpDataPackageDownloader(client);
        await using var destination = new MemoryStream();

        await downloader.DownloadAsync(
            new DataPackageManifest("history", "2", "https://example.test/history.json", new string('0', 64)),
            destination,
            default);

        Assert.Equal("package", Encoding.UTF8.GetString(destination.ToArray()));
    }

    [Fact]
    public async Task RejectsNonHttpsAndFailedResponses()
    {
        var downloader = new HttpDataPackageDownloader(new HttpClient(new Handler(HttpStatusCode.NotFound, "missing")));

        await Assert.ThrowsAsync<InvalidDataException>(() => downloader.DownloadAsync(
            new DataPackageManifest("history", "2", "http://example.test/history.json", new string('0', 64)),
            new MemoryStream(),
            default));
        await Assert.ThrowsAsync<HttpRequestException>(() => downloader.DownloadAsync(
            new DataPackageManifest("history", "2", "https://example.test/history.json", new string('0', 64)),
            new MemoryStream(),
            default));
    }

    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
}
