using System.Windows;
using System.Windows.Input;
using QingLi.Windows.ViewModels;

namespace QingLi.Windows.Views;

public partial class CalendarPopupWindow : Window
{
    private readonly CalendarPopupDeactivationGuard _deactivationGuard =
        new(TimeSpan.FromMilliseconds(750));

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
        _deactivationGuard.MarkShown();
    }

    private void OnDeactivated(object sender, EventArgs e)
    {
        if (_deactivationGuard.ShouldClose())
        {
            Close();
        }
    }

    private void PositionInWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left, workArea.Right - Width - 12);
        Top = Math.Max(workArea.Top, workArea.Bottom - Height - 12);
    }
}
