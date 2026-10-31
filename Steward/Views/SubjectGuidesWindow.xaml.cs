using System.Windows;
using System.Windows.Input;
using Steward.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace Steward.Views;

/// <summary>
/// Окно «Уточнить для предмета»: какие из общих методических указаний и образцов относятся к предмету. Открывается поверх окна приложения и по его центру
/// </summary>
public partial class SubjectGuidesWindow : Window
{
    /// <summary>
    /// Создаёт окно
    /// </summary>
    /// <param name="model">Материалы с галочками</param>
    public SubjectGuidesWindow(SubjectGuidesDialogViewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }

    /// <summary>
    /// Открывает окно по центру окна приложения и ждёт ответа
    /// </summary>
    /// <param name="owner">Окно приложения</param>
    /// <param name="model">Материалы с галочками</param>
    /// <returns>Ключи материалов без галочки или <see langword="null"/>, если окно закрыли без сохранения</returns>
    public static IReadOnlyList<string>? Ask(Window? owner, SubjectGuidesDialogViewModel model)
    {
        var window = new SubjectGuidesWindow(model);
        if (owner is { IsVisible: true })
            window.Owner = owner;
        else
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        return window.ShowDialog() == true ? model.Excluded() : null;
    }

    /// <summary>
    /// Сохраняет выбор
    /// </summary>
    /// <param name="sender">Кнопка</param>
    /// <param name="e">Событие</param>
    private void Confirm_OnClick(object sender, RoutedEventArgs e) => DialogResult = true;

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
