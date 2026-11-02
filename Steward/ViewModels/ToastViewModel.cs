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
    /// Сколько времени осталось, от единицы до нуля
    /// </summary>
    private double _progress = 1;

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
    /// Сколько секунд осталось до исчезновения: число в кольце
    /// </summary>
    public int SecondsLeft
    {
        get => _secondsLeft;
        private set => Set(ref _secondsLeft, value);
    }

    /// <summary>
    /// Сколько времени осталось, от единицы до нуля: от этого убывает заливка окошка
    /// </summary>
    public double Progress
    {
        get => _progress;
        private set
        {
            if (!Set(ref _progress, value))
                return;

            Raise(nameof(RingOffset));
        }
    }

    /// <summary>
    /// Смещение штриха кольца для кольца диаметром 35 и толщиной 3 (длина окружности 36,65 толщины): кольцо опустошается вместе с временем
    /// </summary>
    public double RingOffset => (1 - _progress) * 36.65;

    /// <summary>
    /// Сколько кадров в секунду перерисовывается обратный отсчёт. В тестах секунда короче 100 миллисекунд и считается одним кадром
    /// </summary>
    private int Frames => TickMilliseconds >= 100 ? 10 : 1;

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
        Progress = 1;
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
        var frames = Frames;
        var total = SecondsLeft * frames;
        var left = total;
        while (version == _version && left > 0)
        {
            await Task.Delay(Math.Max(1, TickMilliseconds / frames));
            if (version != _version)
                break;

            left--;
            Progress = (double)left / total;
            SecondsLeft = (left + frames - 1) / frames;
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
