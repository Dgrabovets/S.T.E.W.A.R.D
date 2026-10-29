using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace Steward.Views;

/// <summary>
/// Окно «Поправить условие»: верная формулировка задания и требования. Открывается поверх окна приложения и по его центру
/// </summary>
public partial class ConditionWindow : Window
{
    /// <summary>
    /// Создаёт окно с прежним условием в полях
    /// </summary>
    /// <param name="heading">Занятие, к которому относится задание</param>
    /// <param name="title">Прежняя формулировка задания</param>
    /// <param name="requirements">Прежние требования</param>
    public ConditionWindow(string heading, string title, string requirements)
    {
        InitializeComponent();
        Heading.Text = heading;
        TitleBox.Text = title;
        RequirementsBox.Text = requirements;
        TitleBox.TextChanged += (_, _) =>
        {
            if (TitleError.Visibility != Visibility.Visible || TitleBox.Text.Trim().Length == 0)
                return;

            TitleBox.Style = (Style)FindResource("Field");
            TitleError.Visibility = Visibility.Collapsed;
        };
        Loaded += (_, _) =>
        {
            TitleBox.Focus();
            TitleBox.SelectAll();
        };
    }

    /// <summary>
    /// Открывает окно по центру окна приложения и ждёт ответа
    /// </summary>
    /// <param name="owner">Окно приложения</param>
    /// <param name="heading">Занятие, к которому относится задание</param>
    /// <param name="title">Прежняя формулировка задания</param>
    /// <param name="requirements">Прежние требования</param>
    /// <returns>Новая формулировка и требования или <see langword="null"/>, если окно закрыли без сохранения</returns>
    public static (string Title, string Requirements)? Ask(Window? owner, string heading, string title, string requirements)
    {
        var window = new ConditionWindow(heading, title, requirements);
        if (owner is { IsVisible: true })
            window.Owner = owner;
        else
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        return window.ShowDialog() == true ? (window.TitleBox.Text.Trim(), window.RequirementsBox.Text.Trim()) : null;
    }

    /// <summary>
    /// Сохраняет: без формулировки сохранять нечего
    /// </summary>
    /// <param name="sender">Кнопка</param>
    /// <param name="e">Событие</param>
    private void Confirm_OnClick(object sender, RoutedEventArgs e)
    {
        if (TitleBox.Text.Trim().Length == 0)
        {
            TitleBox.Style = (Style)FindResource("FieldInvalid");
            TitleError.Visibility = Visibility.Visible;
            TitleBox.Focus();

            return;
        }

        DialogResult = true;
    }

    /// <summary>
    /// Закрывает окно без сохранения
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
