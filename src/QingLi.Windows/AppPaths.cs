using System.IO;

namespace QingLi.Windows;

public static class AppPaths
{
    public static string GetDatabasePath(string localApplicationData) =>
        Path.Combine(localApplicationData, "QingLi", "qingli.db");
}
