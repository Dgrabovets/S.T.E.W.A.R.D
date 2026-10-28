using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace Steward.Views;

/// <summary>
/// Окно «Переделать задание»: короткое поле, куда пишут, что не понравилось. Открывается поверх окна приложения и по его центру
/// </summary>
public partial class RedoWindow : Window
{
    /// <summary>
    /// Создаёт окно
    /// </summary>
    /// <param name="heading">Занятие, работа по которому переделывается</param>
    public RedoWindow(string heading)
    {
        InitializeComponent();
        Heading.Text = heading;
        Loaded += (_, _) => Remarks.Focus();
    }

    /// <summary>
    /// Что написал пользователь
    /// </summary>
    public string Text => Remarks.Text.Trim();

    /// <summary>
    /// Открывает окно по центру окна приложения и ждёт ответа
    /// </summary>
    /// <param name="owner">Окно приложения</param>
    /// <param name="heading">Занятие, работа по которому переделывается</param>
    /// <returns>Замечания или <see langword="null"/>, если окно закрыли без переделки</returns>
    public static string? Ask(Window? owner, string heading)
    {
        var window = new RedoWindow(heading);
        if (owner is { IsVisible: true })
            window.Owner = owner;
        else
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        return window.ShowDialog() == true ? window.Text : null;
    }

    /// <summary>
    /// Подтверждает: без замечаний переделывать нечего
    /// </summary>
    /// <param name="sender">Кнопка</param>
    /// <param name="e">Событие</param>
    private void Confirm_OnClick(object sender, RoutedEventArgs e)
    {
        if (Text.Length == 0)
        {
            Remarks.Focus();

            return;
        }

        DialogResult = true;
    }

    /// <summary>
    /// Закрывает окно
    /// </summary>
    /// <param name="sender">Кнопка</param>
    /// <param name="e">Событие</param>
    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

    /// <summary>
    /// Переносит окно за шапку
    /// </summary>
    /// <param name="sender">Шапка</param>
    /// <param name="e">Нажатие мыши</param>
    private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.Button)
            DragMove();
    }

    /// <summary>
    /// Закрывает окно по Esc
    /// </summary>
    /// <param name="sender">Окно</param>
    /// <param name="e">Нажатие клавиши</param>
    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            DialogResult = false;
    }
}
