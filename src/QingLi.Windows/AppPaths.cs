using System.IO;

namespace QingLi.Windows;

public static class AppPaths
{
    public static string DataDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QingLi");

    public static string DatabasePath => Path.Combine(DataDirectory, "qingli.db");

    public static string GetDatabasePath(string localApplicationData) =>
        Path.Combine(localApplicationData, "QingLi", "qingli.db");
}
