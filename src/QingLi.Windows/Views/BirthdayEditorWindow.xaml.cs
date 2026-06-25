using System.Windows;
using QingLi.Core.Birthdays;
using QingLi.Windows.ViewModels;

namespace QingLi.Windows.Views;

public partial class BirthdayEditorWindow : Window
{
    public BirthdayEditorWindow(BirthdayEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private BirthdayEditorViewModel ViewModel => (BirthdayEditorViewModel)DataContext;

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SaveCommand.ExecuteAsync();
        if (ViewModel.ValidationErrors.Count == 0 && ViewModel.SaveCommand.LastError is null)
        {
            DialogResult = true;
        }
    }
}
