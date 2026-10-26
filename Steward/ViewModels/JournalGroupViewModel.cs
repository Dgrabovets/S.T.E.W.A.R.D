using Steward.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Раздел журнала: все запуски одной задачи или все сверки расписания. Раскрывается и сворачивается по нажатию. Объект живёт, пока раздел есть в журнале, а данные в нём обновляются, чтобы свёрнутый раздел не проигрывал анимацию заново
/// </summary>
public sealed class JournalGroupViewModel : Bindable
{
    /// <summary>
    /// Запоминает выбор человека, чтобы обновление данных его не сбросило
    /// </summary>
    private readonly Action<string, bool> _opened;

    /// <summary>
    /// Раздел раскрыт
    /// </summary>
    private bool _isOpen;

    /// <summary>
    /// Короткая сводка под названием
    /// </summary>
    private string _summary = string.Empty;

    /// <summary>
    /// Цвет по последней записи
    /// </summary>
    private PanelToneEnum _tone;

    /// <summary>
    /// Вид сферы по последней записи
    /// </summary>
    private ActivityKindEnum _kind;

    /// <summary>
    /// Запуски задачи от новых к старым
    /// </summary>
    private IReadOnlyList<RunRowViewModel> _runs = [];

    /// <summary>
    /// Сверки расписания от новых к старым
    /// </summary>
    private IReadOnlyList<ScheduleCheckViewModel> _checks = [];

    /// <summary>
    /// Создаёт раздел
    /// </summary>
    /// <param name="id">Идентификатор задачи или «schedule»</param>
    /// <param name="title">Название раздела</param>
    /// <param name="isOpen">Раскрыт ли раздел сначала</param>
    /// <param name="opened">Вызывается, когда человек раскрыл или свернул раздел</param>
    public JournalGroupViewModel(string id, string title, bool isOpen, Action<string, bool> opened)
    {
        Id = id;
        Title = title;
        _isOpen = isOpen;
        _opened = opened;
    }

    /// <summary>
    /// Идентификатор задачи или «schedule»
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Название раздела
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Короткая сводка под названием
    /// </summary>
    public string Summary
    {
        get => _summary;
        private set => Set(ref _summary, value);
    }

    /// <summary>
    /// Цвет по последней записи
    /// </summary>
    public PanelToneEnum Tone
    {
        get => _tone;
        private set => Set(ref _tone, value);
    }

    /// <summary>
    /// Вид сферы по последней записи
    /// </summary>
    public ActivityKindEnum Kind
    {
        get => _kind;
        private set => Set(ref _kind, value);
    }

    /// <summary>
    /// Запуски задачи от новых к старым
    /// </summary>
    public IReadOnlyList<RunRowViewModel> Runs
    {
        get => _runs;
        private set => Set(ref _runs, value);
    }

    /// <summary>
    /// Сверки расписания от новых к старым
    /// </summary>
    public IReadOnlyList<ScheduleCheckViewModel> Checks
    {
        get => _checks;
        private set => Set(ref _checks, value);
    }

    /// <summary>
    /// Раздел раскрыт
    /// </summary>
    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (!Set(ref _isOpen, value))
                return;

            _opened(Id, value);
        }
    }

    /// <summary>
    /// Кладёт в раздел свежие записи
    /// </summary>
    /// <param name="summary">Короткая сводка</param>
    /// <param name="tone">Цвет по последней записи</param>
    /// <param name="kind">Вид сферы по последней записи</param>
    /// <param name="runs">Запуски задачи</param>
    /// <param name="checks">Сверки расписания</param>
    public void Update(string summary, PanelToneEnum tone, ActivityKindEnum kind, IReadOnlyList<RunRowViewModel> runs, IReadOnlyList<ScheduleCheckViewModel> checks)
    {
        Summary = summary;
        Tone = tone;
        Kind = kind;
        Runs = runs;
        Checks = checks;
    }
}
