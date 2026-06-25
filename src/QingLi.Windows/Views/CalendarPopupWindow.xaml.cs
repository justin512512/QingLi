using System.Windows;
using System.Windows.Input;
using QingLi.Windows.ViewModels;

namespace QingLi.Windows.Views;

public partial class CalendarPopupWindow : Window
{
    public CalendarPopupWindow(CalendarPopupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionInWorkArea();
        Activate();
        Focus();
    }

    private void OnDeactivated(object sender, EventArgs e) => Close();

    private void PositionInWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left, workArea.Right - Width - 12);
        Top = Math.Max(workArea.Top, workArea.Bottom - Height - 12);
    }
}
