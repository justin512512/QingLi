using System.Windows;
using QingLi.Windows.ViewModels;

namespace QingLi.Windows.Views;

public partial class BirthdayManagerWindow : Window
{
    private readonly Func<BirthdayEditorViewModel, BirthdayEditorWindow> _editorFactory;
    private readonly Func<string, string, MessageBoxResult> _confirmDelete;

    public BirthdayManagerWindow(
        BirthdayManagerViewModel viewModel,
        Func<BirthdayEditorViewModel, BirthdayEditorWindow>? editorFactory = null,
        Func<string, string, MessageBoxResult>? confirmDelete = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        _editorFactory = editorFactory ?? (editorViewModel => new BirthdayEditorWindow(editorViewModel));
        _confirmDelete = confirmDelete ?? ConfirmDelete;
        Loaded += OnLoaded;
    }

    private BirthdayManagerViewModel ViewModel => (BirthdayManagerViewModel)DataContext;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ViewModel.LoadCommand.ExecuteAsync();
    }

    private async void OnSearchClick(object sender, RoutedEventArgs e) =>
        await ViewModel.SearchCommand.ExecuteAsync();

    private async void OnAddClick(object sender, RoutedEventArgs e)
    {
        var window = _editorFactory(new BirthdayEditorViewModel(GetRepository()));
        window.Owner = this;
        if (window.ShowDialog() == true)
        {
            await ViewModel.LoadCommand.ExecuteAsync();
        }
    }

    private async void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedBirthday?.Birthday is null)
        {
            return;
        }

        var window = _editorFactory(new BirthdayEditorViewModel(GetRepository(), birthday: ViewModel.SelectedBirthday.Birthday));
        window.Owner = this;
        if (window.ShowDialog() == true)
        {
            await ViewModel.LoadCommand.ExecuteAsync();
        }
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedBirthday is null)
        {
            return;
        }

        var result = _confirmDelete(
            "删除后不可恢复，确定继续吗？",
            "删除生日");

        if (result == MessageBoxResult.Yes)
        {
            await ViewModel.DeleteSelectedCommand.ExecuteAsync();
        }
    }

    private static MessageBoxResult ConfirmDelete(string message, string caption) =>
        System.Windows.MessageBox.Show(
            message,
            caption,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

    private QingLi.Core.Birthdays.IBirthdayRepository GetRepository() =>
        ((App)System.Windows.Application.Current).BirthdayRepository;
}
