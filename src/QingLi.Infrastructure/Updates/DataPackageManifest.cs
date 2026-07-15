namespace QingLi.Infrastructure.Updates;

public sealed record DataPackageManifest(
    string PackageName,
    string Version,
    string DownloadUrl,
    string Sha256);
