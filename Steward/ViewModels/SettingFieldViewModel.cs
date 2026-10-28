using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Одно поле файла настроек процесса: подпись, пояснение и значение, которое человек правит на странице
/// </summary>
/// <param name="path">Путь к значению в JSON: ключи через точку, например «Mail.Login»</param>
/// <param name="title">Подпись поля</param>
/// <param name="hint">Пояснение под подписью</param>
/// <param name="secret">
/// <see langword="true"/>, если значение закрыто точками: пароли и токены
/// </param>
/// <param name="number">
/// <see langword="true"/>, если в файл нужно записать число
/// </param>
public sealed class SettingFieldViewModel(string path, string title, string hint, bool secret = false, bool number = false) : Bindable
{
    /// <summary>
    /// Введённое значение
    /// </summary>
    private string _value = string.Empty;

    /// <summary>
    /// Путь к значению в JSON
    /// </summary>
    public string Path { get; } = path;

    /// <summary>
    /// Подпись поля
    /// </summary>
    public string Title { get; } = title;

    /// <summary>
    /// Пояснение под подписью
    /// </summary>
    public string Hint { get; } = hint;

    /// <summary>
    /// Значение закрыто точками
    /// </summary>
    public bool IsSecret { get; } = secret;

    /// <summary>
    /// Значение записывается числом
    /// </summary>
    public bool IsNumber { get; } = number;

    /// <summary>
    /// Значение открыто на экране
    /// </summary>
    public bool IsPlain => !IsSecret;

    /// <summary>
    /// Введённое значение
    /// </summary>
    public string Value
    {
        get => _value;
        set => Set(ref _value, value);
    }
}
