using System.Windows;

namespace Steward.Views;

/// <summary>
/// Вопрос в стиле приложения с двумя кнопками: подтвердить и отказаться
/// </summary>
public partial class ConfirmWindow : Window
{
    /// <summary>
    /// Создаёт окно вопроса
    /// </summary>
    /// <param name="heading">Сам вопрос</param>
    /// <param name="body">Пояснение под вопросом</param>
    /// <param name="confirm">Подпись кнопки подтверждения</param>
    /// <param name="cancel">Подпись кнопки отказа</param>
    public ConfirmWindow(string heading, string body, string confirm, string cancel)
    {
        InitializeComponent();
        Heading.Text = heading;
        Body.Text = body;
        Confirm.Content = confirm;
        Cancel.Content = cancel;
    }

    /// <summary>
    /// Спрашивает и ждёт ответа
    /// </summary>
    /// <param name="heading">Сам вопрос</param>
    /// <param name="body">Пояснение под вопросом</param>
    /// <param name="confirm">Подпись кнопки подтверждения</param>
    /// <param name="cancel">Подпись кнопки отказа</param>
    /// <returns>
    /// <see langword="true"/>, если выбрано подтверждение
    /// </returns>
    public static bool Ask(string heading, string body, string confirm, string cancel) =>
        new ConfirmWindow(heading, body, confirm, cancel).ShowDialog() == true;

    /// <summary>
    /// Подтверждает
    /// </summary>
    /// <param name="sender">Кнопка</param>
    /// <param name="e">Аргументы нажатия</param>
    private void Confirm_OnClick(object sender, RoutedEventArgs e) => DialogResult = true;

    /// <summary>
    /// Отказывается
    /// </summary>
    /// <param name="sender">Кнопка</param>
    /// <param name="e">Аргументы нажатия</param>
    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;
}