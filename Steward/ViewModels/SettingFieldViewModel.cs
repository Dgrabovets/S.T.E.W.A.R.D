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
/// <param name="toggle">
/// <see langword="true"/>, если значение — переключатель «да или нет», который пишется в файл как <c>true</c> или <c>false</c>
/// </param>
public sealed class SettingFieldViewModel(string path, string title, string hint, bool secret = false, bool number = false, bool toggle = false) : Bindable
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
    /// Значение — переключатель, записывается как <c>true</c> или <c>false</c>
    /// </summary>
    public bool IsToggle { get; } = toggle;

    /// <summary>
    /// У поля есть пояснение
    /// </summary>
    public bool HasHint => Hint.Length > 0;

    /// <summary>
    /// Значение открыто на экране в поле ввода
    /// </summary>
    public bool IsPlain => !IsSecret && !IsToggle;

    /// <summary>
    /// Над полем нужна отдельная подпись: у переключателя подпись стоит рядом с ним
    /// </summary>
    public bool HasTitleAbove => !IsToggle;

    /// <summary>
    /// Введённое значение. У переключателя это <c>true</c>, <c>false</c> или пусто, если ключа в файле нет
    /// </summary>
    public string Value
    {
        get => _value;
        set
        {
            if (Set(ref _value, value))
                Raise(nameof(IsOn));
        }
    }

    /// <summary>
    /// Переключатель включён. Пустое или непонятное значение считается выключенным
    /// </summary>
    public bool IsOn
    {
        get => bool.TryParse(_value.Trim(), out var on) && on;
        set => Value = value ? "true" : "false";
    }
}
