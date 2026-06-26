using System.Windows;
using QingLi.Windows.ViewModels;

namespace QingLi.Windows.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ViewModel.LoadCommand.ExecuteAsync();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SaveCommand.ExecuteAsync();
        if (ViewModel.CanCloseAfterSave)
        {
            Close();
        }
    }

    private async void OnOpenDataDirectoryClick(object sender, RoutedEventArgs e) =>
        await ViewModel.OpenDataDirectoryCommand.ExecuteAsync();
}
