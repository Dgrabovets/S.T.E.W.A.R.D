using Steward.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Общая часть окон приложения: заголовок, цветная плашка с итогом по задачам и выход
/// </summary>
public abstract class PanelViewModel : Bindable
{
    /// <summary>
    /// Строка под заголовком
    /// </summary>
    private string _subtitle = "Проверяю задачи";

    /// <summary>
    /// Текст плашки
    /// </summary>
    private string _statusLabel;

    /// <summary>
    /// Цвет плашки
    /// </summary>
    private PanelToneEnum _tone = PanelToneEnum.Idle;

    /// <summary>
    /// Создаёт основу окна
    /// </summary>
    /// <param name="statusLabel">Текст плашки, пока итога по задачам ещё нет</param>
    protected PanelViewModel(string statusLabel)
    {
        _statusLabel = statusLabel;
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>
    /// Просит закрыть приложение
    /// </summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    /// Закрывает приложение целиком
    /// </summary>
    public RelayCommand ExitCommand { get; }

    /// <summary>
    /// Строка под заголовком окна: всё ли в порядке или что требует внимания
    /// </summary>
    public string Subtitle
    {
        get => _subtitle;
        private set => Set(ref _subtitle, value);
    }

    /// <summary>
    /// Текст цветной плашки
    /// </summary>
    public string StatusLabel
    {
        get => _statusLabel;
        private set => Set(ref _statusLabel, value);
    }

    /// <summary>
    /// Цвет плашки
    /// </summary>
    public PanelToneEnum Tone
    {
        get => _tone;
        private set
        {
            if (Set(ref _tone, value))
                OnToneChanged();
        }
    }

    /// <summary>
    /// Показывает общий итог по строкам задач
    /// </summary>
    /// <param name="rows">Строки задач</param>
    protected void ApplySummary(IReadOnlyList<TaskStatusRow> rows)
    {
        var summary = TaskSummary.Of(rows);
        Tone = summary.Tone;
        StatusLabel = summary.Label;
        Subtitle = summary.Subtitle;
    }

    /// <summary>
    /// Вызывается, когда цвет плашки изменился: наследник сообщает окну о свойствах, которые от него зависят
    /// </summary>
    protected virtual void OnToneChanged()
    {
    }
}