using System.Windows;
using System.Windows.Media;

namespace Steward.Views;

/// <summary>
/// Меню иконки в трее в стилистике приложения: открыть окно или выйти. Появляется у курсора и закрывается, когда фокус уходит
/// </summary>
public partial class TrayMenuPopup : Window
{
    /// <summary>
    /// Открывает главное окно
    /// </summary>
    private readonly Action _open;

    /// <summary>
    /// Завершает приложение
    /// </summary>
    private readonly Action _exit;

    /// <summary>
    /// Создаёт меню
    /// </summary>
    /// <param name="open">Вызывается по пункту «Открыть Steward»</param>
    /// <param name="exit">Вызывается по пункту «Выход»</param>
    public TrayMenuPopup(Action open, Action exit)
    {
        InitializeComponent();
        _open = open;
        _exit = exit;
        SourceInitialized += (_, _) => Steward.Shell.WindowCorners.Round(this);
        Deactivated += (_, _) => Close();
        KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
                Close();
        };
    }

    /// <summary>
    /// Показывает меню над курсором, у правого края, и не даёт ему выйти за рабочую область
    /// </summary>
    public void ShowAtCursor()
    {
        Show();
        UpdateLayout();

        var scale = VisualTreeHelper.GetDpi(this);
        var cursor = System.Windows.Forms.Cursor.Position;
        var area = SystemParameters.WorkArea;
        var x = (cursor.X / scale.DpiScaleX) - ActualWidth;
        var y = (cursor.Y / scale.DpiScaleY) - ActualHeight - 8;
        Left = Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - ActualWidth));
        Top = Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - ActualHeight));
        Activate();
    }

    /// <summary>
    /// Закрывает меню и открывает главное окно
    /// </summary>
    /// <param name="sender">Пункт меню</param>
    /// <param name="e">Данные события</param>
    private void Open_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
        _open();
    }

    /// <summary>
    /// Закрывает меню и завершает приложение
    /// </summary>
    /// <param name="sender">Пункт меню</param>
    /// <param name="e">Данные события</param>
    private void Exit_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
        _exit();
    }
}
