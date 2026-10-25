using System.Windows;
using System.Windows.Controls;

namespace Steward.Behaviors;

/// <summary>
/// Связывает скрытое поле пароля со свойством модели представления
/// </summary>
public static class PasswordBinding
{
    /// <summary>
    /// Текст, который лежит в поле. На экране он закрыт точками
    /// </summary>
    public static readonly DependencyProperty PasswordProperty = DependencyProperty.RegisterAttached(
        "Password",
        typeof(string),
        typeof(PasswordBinding),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPasswordChanged));

    #region Transfer

    /// <summary>
    /// Читает связанный текст
    /// </summary>
    /// <param name="element">Поле пароля</param>
    /// <returns>Текущий текст</returns>
    public static string GetPassword(DependencyObject element) => (string)element.GetValue(PasswordProperty);

    /// <summary>
    /// Записывает текст в поле
    /// </summary>
    /// <param name="element">Поле пароля</param>
    /// <param name="value">Текст из модели представления</param>
    public static void SetPassword(DependencyObject element, string value) => element.SetValue(PasswordProperty, value);

    /// <summary>
    /// Переносит текст из модели в поле, не зацикливая событие ввода
    /// </summary>
    /// <param name="element">Поле пароля</param>
    /// <param name="args">Старое и новое значение</param>
    private static void OnPasswordChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not PasswordBox box)
            return;

        box.PasswordChanged -= OnBoxChanged;
        var password = args.NewValue as string ?? string.Empty;
        if (box.Password != password)
            box.Password = password;

        box.PasswordChanged += OnBoxChanged;
    }

    /// <summary>
    /// Переносит введённый текст обратно в модель
    /// </summary>
    /// <param name="sender">Поле пароля</param>
    /// <param name="args">Аргументы события</param>
    private static void OnBoxChanged(object sender, RoutedEventArgs args)
    {
        var box = (PasswordBox)sender;
        SetPassword(box, box.Password);
    }

    #endregion Transfer
}
