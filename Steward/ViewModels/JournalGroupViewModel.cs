using Steward.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Раздел журнала: все запуски одной задачи или все сверки расписания. Раскрывается и сворачивается по нажатию. Объект живёт, пока раздел есть в журнале, а данные в нём обновляются только когда изменились, чтобы окно не перестраивалось при каждом опросе. Показывает по несколько записей, остальные подгружаются кнопкой
/// </summary>
public sealed class JournalGroupViewModel : Bindable
{
    /// <summary>
    /// Сколько записей показывается сразу и сколько добавляет кнопка «Показать ещё»
    /// </summary>
    public const int PAGE = 10;

    /// <summary>
    /// Запоминает выбор человека, чтобы обновление данных его не сбросило
    /// </summary>
    private readonly Action<string, bool> _opened;

    /// <summary>
    /// Раздел раскрыт
    /// </summary>
    private bool _isOpen;

    /// <summary>
    /// Сколько записей сейчас показано
    /// </summary>
    private int _shown = PAGE;

    /// <summary>
    /// Отпечаток последних данных: если он не изменился, обновлять нечего
    /// </summary>
    private string _signature = string.Empty;

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
    /// Все запуски задачи от новых к старым
    /// </summary>
    private IReadOnlyList<RunRowViewModel> _allRuns = [];

    /// <summary>
    /// Все сверки расписания от новых к старым
    /// </summary>
    private IReadOnlyList<ScheduleCheckViewModel> _allChecks = [];

    /// <summary>
    /// Показанные запуски
    /// </summary>
    private IReadOnlyList<RunRowViewModel> _runs = [];

    /// <summary>
    /// Показанные сверки
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
        ShowMoreCommand = new RelayCommand(ShowMore);
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
    /// Показывает ещё одну страницу записей
    /// </summary>
    public RelayCommand ShowMoreCommand { get; }

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
    /// Показанные запуски задачи от новых к старым
    /// </summary>
    public IReadOnlyList<RunRowViewModel> Runs
    {
        get => _runs;
        private set => Set(ref _runs, value);
    }

    /// <summary>
    /// Показанные сверки расписания от новых к старым
    /// </summary>
    public IReadOnlyList<ScheduleCheckViewModel> Checks
    {
        get => _checks;
        private set => Set(ref _checks, value);
    }

    /// <summary>
    /// Есть записи, которые ещё не показаны
    /// </summary>
    public bool HasMore => Hidden > 0;

    /// <summary>
    /// Подпись кнопки подгрузки
    /// </summary>
    public string MoreLabel => $"Показать ещё ({Math.Min(PAGE, Hidden)} из {Hidden})";

    /// <summary>
    /// Сколько записей не показано
    /// </summary>
    private int Hidden => Math.Max(_allRuns.Count, _allChecks.Count) - _shown;

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
    /// Проверяет, что в разделе уже лежат именно эти данные
    /// </summary>
    /// <param name="signature">Отпечаток данных</param>
    /// <returns>
    /// <see langword="true"/>, если обновлять нечего
    /// </returns>
    public bool IsCurrent(string signature) => signature == _signature;

    /// <summary>
    /// Кладёт в раздел свежие записи, если они отличаются от прежних
    /// </summary>
    /// <param name="signature">Отпечаток данных: при том же отпечатке ничего не меняется</param>
    /// <param name="summary">Короткая сводка</param>
    /// <param name="tone">Цвет по последней записи</param>
    /// <param name="kind">Вид сферы по последней записи</param>
    /// <param name="runs">Запуски задачи</param>
    /// <param name="checks">Сверки расписания</param>
    public void Update(string signature, string summary, PanelToneEnum tone, ActivityKindEnum kind, IReadOnlyList<RunRowViewModel> runs, IReadOnlyList<ScheduleCheckViewModel> checks)
    {
        if (signature == _signature)
            return;

        _signature = signature;
        _allRuns = runs;
        _allChecks = checks;
        Summary = summary;
        Tone = tone;
        Kind = kind;
        Refresh();
    }

    /// <summary>
    /// Добавляет страницу записей
    /// </summary>
    private void ShowMore()
    {
        _shown += PAGE;
        Refresh();
    }

    /// <summary>
    /// Пересобирает показанные записи и сообщает окну о кнопке подгрузки
    /// </summary>
    private void Refresh()
    {
        Runs = [.. _allRuns.Take(_shown)];
        Checks = [.. _allChecks.Take(_shown)];
        Raise(nameof(HasMore));
        Raise(nameof(MoreLabel));
    }
}
