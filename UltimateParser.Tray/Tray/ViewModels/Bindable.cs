using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UltimateParser.Tray.Tray.ViewModels;

/// <summary>
/// Основа модели представления: сообщает окну, что свойство изменилось
/// </summary>
public abstract class Bindable : INotifyPropertyChanged
{
    /// <summary>
    /// Окно подписано на это событие и обновляет связанные поля
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    #region Notification

    /// <summary>
    /// Записывает значение и уведомляет окно, если оно отличается от прежнего
    /// </summary>
    /// <typeparam name="T">
    /// Тип свойства
    /// </typeparam>
    /// <param name="field">
    /// Поле, в котором лежит значение
    /// </param>
    /// <param name="value">
    /// Новое значение
    /// </param>
    /// <param name="name">
    /// Имя свойства. Подставляется компилятором
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если значение изменилось
    /// </returns>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        Raise(name);
        return true;
    }

    /// <summary>
    /// Сообщает окну, что свойство изменилось
    /// </summary>
    /// <param name="name">
    /// Имя свойства. Подставляется компилятором
    /// </param>
    protected void Raise([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    #endregion Notification
}
