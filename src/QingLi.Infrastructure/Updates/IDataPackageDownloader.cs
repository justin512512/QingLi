namespace QingLi.Infrastructure.Updates;

public interface IDataPackageDownloader
{
    Task DownloadAsync(
        DataPackageManifest manifest,
        Stream destination,
        CancellationToken cancellationToken);
}
