using System.Windows;
using QingLi.Windows.ViewModels;

namespace QingLi.Windows.Views;

public partial class AnniversaryEditorWindow : Window
{
    public AnniversaryEditorWindow(AnniversaryEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private AnniversaryEditorViewModel ViewModel => (AnniversaryEditorViewModel)DataContext;

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
