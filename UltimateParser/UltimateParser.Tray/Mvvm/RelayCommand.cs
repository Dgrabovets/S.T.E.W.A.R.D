using System.Windows.Input;

namespace UltimateParser.Tray.Mvvm;

/// <summary>
/// Команда кнопки: вызывает действие модели представления
/// </summary>
public sealed class RelayCommand : ICommand
{
    /// <summary>
    /// Действие по нажатию
    /// </summary>
    private readonly Action _execute;

    /// <summary>
    /// Проверка, можно ли нажать кнопку
    /// </summary>
    private readonly Func<bool>? _canExecute;

    /// <summary>
    /// Создаёт команду
    /// </summary>
    /// <param name="execute">
    /// Действие по нажатию
    /// </param>
    /// <param name="canExecute">
    /// Проверка доступности. Пусто, если кнопка всегда доступна
    /// </param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <summary>
    /// Кнопка переспрашивает, можно ли её нажать
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    #region Execution

    /// <summary>
    /// Проверяет, можно ли выполнить команду
    /// </summary>
    /// <param name="parameter">
    /// Аргумент кнопки. Эта команда его не использует
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если кнопку можно нажать
    /// </returns>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <summary>
    /// Выполняет действие кнопки
    /// </summary>
    /// <param name="parameter">
    /// Аргумент кнопки. Эта команда его не использует
    /// </param>
    public void Execute(object? parameter) => _execute();

    #endregion Execution
}
