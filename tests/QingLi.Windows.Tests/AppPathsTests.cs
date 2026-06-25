using QingLi.Windows;

namespace QingLi.Windows.Tests;

public sealed class AppPathsTests
{
    [Fact]
    public void Database_path_is_fixed_under_local_application_data()
    {
        var actual = AppPaths.GetDatabasePath(@"C:\Users\Example\AppData\Local");

        Assert.Equal(@"C:\Users\Example\AppData\Local\QingLi\qingli.db", actual);
    }

    [Fact]
    public void Data_directory_is_fixed_under_local_application_data()
    {
        Assert.Equal(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QingLi"),
            AppPaths.DataDirectory);
    }
}
