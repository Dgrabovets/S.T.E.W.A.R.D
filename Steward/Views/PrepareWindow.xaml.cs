using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Steward.ViewModels;

namespace Steward.Views;

/// <summary>
/// Окно «Подготовить задание»: формат работы, дополнительное указание и методические указания с образцами, которые нужно учесть. Открывается поверх окна приложения и по его центру
/// </summary>
public partial class PrepareWindow : Window
{
    /// <summary>
    /// Создаёт окно
    /// </summary>
    /// <param name="model">Что можно выбрать</param>
    public PrepareWindow(PrepareDialogViewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }

    /// <summary>
    /// Открывает окно по центру окна приложения и ждёт ответа
    /// </summary>
    /// <param name="owner">Окно приложения</param>
    /// <param name="model">Что можно выбрать</param>
    /// <returns>
    /// <see langword="true"/>, если нажато «Подготовить»
    /// </returns>
    public static bool Ask(Window? owner, PrepareDialogViewModel model)
    {
        var window = new PrepareWindow(model);
        if (owner is { IsVisible: true })
            window.Owner = owner;
        else
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        return window.ShowDialog() == true;
    }

    /// <summary>
    /// Подтверждает выбор
    /// </summary>
    /// <param name="sender">Кнопка</param>
    /// <param name="e">Событие</param>
    private void Confirm_OnClick(object sender, RoutedEventArgs e) => DialogResult = true;

    /// <summary>
    /// Закрывает окно без подготовки
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
