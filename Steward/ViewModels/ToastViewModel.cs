using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Всплывающее окошко внизу приложения: коротко сообщает о сделанном, при желании даёт откатить действие и гаснет само. Пока оно показано, новое сообщение заменяет прежнее
/// </summary>
public sealed class ToastViewModel : Bindable
{
    /// <summary>
    /// Сколько секунд окошко с откатом остаётся на экране
    /// </summary>
    public const int UNDO_SECONDS = 15;

    /// <summary>
    /// Сколько секунд остаётся окошко без отката
    /// </summary>
    public const int NOTICE_SECONDS = 5;

    /// <summary>
    /// Откатывает показанное действие. Пусто, если откат не нужен
    /// </summary>
    private Action? _undo;

    /// <summary>
    /// Текст сообщения
    /// </summary>
    private string _text = string.Empty;

    /// <summary>
    /// Окошко показано
    /// </summary>
    private bool _isVisible;

    /// <summary>
    /// Сколько секунд осталось до исчезновения
    /// </summary>
    private int _secondsLeft;

    /// <summary>
    /// Номер последнего показа: по нему старый таймер не гасит свежее окошко
    /// </summary>
    private int _version;

    /// <summary>
    /// Создаёт окошко
    /// </summary>
    public ToastViewModel()
    {
        UndoCommand = new RelayCommand(Undo, () => _undo is not null);
        CloseCommand = new RelayCommand(Hide);
    }

    /// <summary>
    /// Сколько миллисекунд длится одна секунда обратного отсчёта. В тестах уменьшается
    /// </summary>
    public int TickMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Текст сообщения
    /// </summary>
    public string Text
    {
        get => _text;
        private set => Set(ref _text, value);
    }

    /// <summary>
    /// Окошко показано
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => Set(ref _isVisible, value);
    }

    /// <summary>
    /// У сообщения есть кнопка «Откатить»
    /// </summary>
    public bool HasUndo => _undo is not null;

    /// <summary>
    /// Сколько секунд осталось до исчезновения: для подсказки на кнопке
    /// </summary>
    public int SecondsLeft
    {
        get => _secondsLeft;
        private set => Set(ref _secondsLeft, value);
    }

    /// <summary>
    /// Откатывает действие и закрывает окошко
    /// </summary>
    public RelayCommand UndoCommand { get; }

    /// <summary>
    /// Закрывает окошко: действие остаётся как есть
    /// </summary>
    public RelayCommand CloseCommand { get; }

    /// <summary>
    /// Показывает сообщение вместо прежнего и запускает обратный отсчёт
    /// </summary>
    /// <param name="text">Что сделано</param>
    /// <param name="undo">Откатывает действие. Пусто: кнопки «Откатить» нет, а окошко гаснет быстрее</param>
    /// <returns>Задача отсчёта, которая завершается, когда окошко погасло или его закрыли; ошибок не выбрасывает</returns>
    public Task ShowAsync(string text, Action? undo = null)
    {
        var version = ++_version;
        _undo = undo;
        Text = text;
        SecondsLeft = undo is null ? NOTICE_SECONDS : UNDO_SECONDS;
        IsVisible = true;
        Raise(nameof(HasUndo));
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();

        return CountDownAsync(version);
    }

    /// <summary>
    /// Показывает сообщение и не ждёт конца отсчёта
    /// </summary>
    /// <param name="text">Что сделано</param>
    /// <param name="undo">Откатывает действие. Пусто: кнопки «Откатить» нет</param>
    public void Show(string text, Action? undo = null) => _ = ShowAsync(text, undo);

    /// <summary>
    /// Считает секунды и гасит окошко, если за это время его не показали заново и не закрыли
    /// </summary>
    /// <param name="version">Номер показа, который гасится</param>
    /// <returns>Задача отсчёта</returns>
    private async Task CountDownAsync(int version)
    {
        while (version == _version && SecondsLeft > 0)
        {
            await Task.Delay(TickMilliseconds);
            if (version == _version)
                SecondsLeft--;
        }

        if (version == _version)
            Hide();
    }

    /// <summary>
    /// Откатывает действие один раз и закрывает окошко
    /// </summary>
    private void Undo()
    {
        var undo = _undo;
        Hide();
        undo?.Invoke();
    }

    /// <summary>
    /// Прячет окошко и останавливает отсчёт
    /// </summary>
    private void Hide()
    {
        _version++;
        _undo = null;
        IsVisible = false;
        Raise(nameof(HasUndo));
    }
}
