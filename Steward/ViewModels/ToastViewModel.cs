using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Всплывающее окошко внизу приложения: коротко сообщает о сделанном, даёт откатить действие и гаснет само. Действие считается окончательным, когда окошко закончилось любым способом, кроме отката: время вышло, окошко закрыли, показали другое или приложение закрывается
/// </summary>
public sealed class ToastViewModel : Bindable
{
    /// <summary>
    /// Сколько секунд окошко остаётся на экране
    /// </summary>
    public const int SECONDS = 5;

    /// <summary>
    /// Откатывает показанное действие. Пусто, если откат не нужен
    /// </summary>
    private Action? _undo;

    /// <summary>
    /// Завершает действие окончательно. Вызывается один раз, если действие не откатили
    /// </summary>
    private Action? _commit;

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
    /// Сколько секунд осталось до исчезновения: число в кольце
    /// </summary>
    public int SecondsLeft
    {
        get => _secondsLeft;
        private set => Set(ref _secondsLeft, value);
    }

    /// <summary>
    /// Номер показа: растёт при каждом новом сообщении, и по нему окно заново запускает плавную анимацию заливки и кольца
    /// </summary>
    public int Version => _version;

    /// <summary>
    /// Сколько секунд длится отсчёт показанного сообщения
    /// </summary>
    public int TotalSeconds { get; private set; }

    /// <summary>
    /// Откатывает действие и закрывает окошко
    /// </summary>
    public RelayCommand UndoCommand { get; }

    /// <summary>
    /// Закрывает окошко: действие становится окончательным
    /// </summary>
    public RelayCommand CloseCommand { get; }

    /// <summary>
    /// Показывает сообщение вместо прежнего и запускает обратный отсчёт. Прежнее сообщение при этом заканчивается, и его действие становится окончательным
    /// </summary>
    /// <param name="text">Что сделано</param>
    /// <param name="undo">Откатывает действие. Пусто: кнопки «Откатить» нет, а окошко гаснет быстрее</param>
    /// <param name="commit">Завершает действие окончательно, когда окошко закончилось без отката</param>
    /// <returns>Задача отсчёта, которая завершается, когда окошко погасло или его закрыли</returns>
    public Task ShowAsync(string text, Action? undo = null, Action? commit = null)
    {
        var previous = _commit;
        var version = ++_version;
        _undo = undo;
        _commit = commit;
        Text = text;
        TotalSeconds = SECONDS;
        SecondsLeft = TotalSeconds;
        IsVisible = true;
        Raise(nameof(HasUndo));
        Raise(nameof(TotalSeconds));
        Raise(nameof(Version));
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        previous?.Invoke();

        return CountDownAsync(version);
    }

    /// <summary>
    /// Показывает сообщение и не ждёт конца отсчёта
    /// </summary>
    /// <param name="text">Что сделано</param>
    /// <param name="undo">Откатывает действие. Пусто: кнопки «Откатить» нет</param>
    /// <param name="commit">Завершает действие окончательно, когда окошко закончилось без отката</param>
    public void Show(string text, Action? undo = null, Action? commit = null) => _ = ShowAsync(text, undo, commit);

    /// <summary>
    /// Заканчивает показанное сообщение сейчас: действие становится окончательным. Вызывается при закрытии приложения, чтобы не потерять действие, которое ещё ждёт отката
    /// </summary>
    public void Flush() => Hide();

    /// <summary>
    /// Считает секунды и гасит окошко, если за это время его не показали заново и не закрыли. Плавность заливки и кольца рисует окно анимацией, здесь меняется только число
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
    /// Откатывает действие один раз и закрывает окошко без окончательного завершения
    /// </summary>
    private void Undo()
    {
        var undo = _undo;
        _commit = null;
        Hide();
        undo?.Invoke();
    }

    /// <summary>
    /// Прячет окошко, останавливает отсчёт и завершает действие окончательно, если его не откатили
    /// </summary>
    private void Hide()
    {
        var commit = _commit;
        _version++;
        _commit = null;
        _undo = null;
        IsVisible = false;
        Raise(nameof(HasUndo));
        commit?.Invoke();
    }
}
