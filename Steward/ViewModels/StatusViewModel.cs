using Steward.Models;
using Steward.Monitoring.Models;

namespace Steward.ViewModels;

/// <summary>
/// Состояние окна истории сверок расписания: журнал и цвет значка
/// </summary>
public sealed class StatusViewModel : PanelViewModel
{
    /// <summary>
    /// Карточки журнала
    /// </summary>
    private IReadOnlyList<JournalCard> _cards = [];

    /// <summary>
    /// Текст, если карточек нет
    /// </summary>
    private string _emptyText = "Журнал пока пуст";

    /// <summary>
    /// Создаёт модель
    /// </summary>
    public StatusViewModel() : base("Остановлено")
    {
    }

    #region State

    /// <summary>
    /// Контейнер работает и сбоя нет. От этого зависит свечение значка
    /// </summary>
    public bool IsRunning => Tone == PanelToneEnum.Running;

    /// <summary>
    /// Карточки журнала от новых к старым
    /// </summary>
    public IReadOnlyList<JournalCard> Cards
    {
        get => _cards;
        private set
        {
            if (!Set(ref _cards, value))
                return;

            Raise(nameof(IsJournalEmpty));
        }
    }

    /// <summary>
    /// Что показать, если журнал пуст
    /// </summary>
    public string EmptyText
    {
        get => _emptyText;
        private set => Set(ref _emptyText, value);
    }

    /// <summary>
    /// Журнал пуст, вместо карточек нужна поясняющая строка
    /// </summary>
    public bool IsJournalEmpty => Cards.Count == 0;

    #endregion State

    #region Snapshot

    /// <summary>
    /// Показывает свежий снимок журнала расписания
    /// </summary>
    /// <param name="snapshot">Текущее состояние синхронизации</param>
    public void Apply(SyncSnapshot snapshot)
    {
        var panel = PanelStatus.From(snapshot);

        ApplySummary(TaskRows.Build(snapshot, []));
        EmptyText = panel.EmptyText;
        Cards = panel.Cards;
    }

    #endregion Snapshot

    #region Overrides

    /// <summary>
    /// От цвета плашки зависит свечение значка
    /// </summary>
    protected override void OnToneChanged() => Raise(nameof(IsRunning));

    #endregion Overrides
}
