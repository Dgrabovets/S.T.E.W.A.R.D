using System.Windows.Input;
using Steward.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Раздел журнала: все запуски одной задачи или все сверки расписания. Раскрывается и сворачивается по нажатию. Объект живёт, пока раздел есть в журнале, а данные в нём обновляются только когда изменились, чтобы окно не перестраивалось при каждом опросе. Показывает записи по страницам: размер страницы выбирается, листать можно стрелками
/// </summary>
public sealed class JournalGroupViewModel : Bindable
{
    /// <summary>
    /// Размер страницы по умолчанию
    /// </summary>
    public const int DEFAULT_PAGE_SIZE = 10;

    /// <summary>
    /// Какие размеры страницы можно выбрать
    /// </summary>
    public static readonly IReadOnlyList<int> PageSizes = [5, 10, 15];

    /// <summary>
    /// Запоминает выбор человека, чтобы обновление данных его не сбросило
    /// </summary>
    private readonly Action<string, bool> _opened;

    /// <summary>
    /// Раздел раскрыт
    /// </summary>
    private bool _isOpen;

    /// <summary>
    /// Сколько записей на странице
    /// </summary>
    private int _pageSize = DEFAULT_PAGE_SIZE;

    /// <summary>
    /// Номер показанной страницы с нуля
    /// </summary>
    private int _pageIndex;

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
    /// <param name="clear">Вызывается, когда человек просит стереть записи раздела</param>
    public JournalGroupViewModel(string id, string title, bool isOpen, Action<string, bool> opened, Action<string> clear)
    {
        Id = id;
        Title = title;
        _isOpen = isOpen;
        _opened = opened;
        PreviousCommand = new RelayCommand(() => PageIndex--, () => CanGoBack);
        NextCommand = new RelayCommand(() => PageIndex++, () => CanGoForward);
        ClearCommand = new RelayCommand(() => clear(id));
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
    /// Стирает записи только этого раздела
    /// </summary>
    public RelayCommand ClearCommand { get; }

    /// <summary>
    /// Переходит на страницу назад
    /// </summary>
    public RelayCommand PreviousCommand { get; }

    /// <summary>
    /// Переходит на страницу вперёд
    /// </summary>
    public RelayCommand NextCommand { get; }

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
    /// Сколько записей на странице. При смене страница пересчитывается так, чтобы первая видимая запись осталась на экране
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set
        {
            if (value <= 0 || value == _pageSize)
                return;

            var first = _pageIndex * _pageSize;
            _pageSize = value;
            _pageIndex = first / value;
            Raise(nameof(PageSize));
            Refresh();
        }
    }

    /// <summary>
    /// Номер показанной страницы с нуля
    /// </summary>
    public int PageIndex
    {
        get => _pageIndex;
        set
        {
            var clamped = Math.Clamp(value, 0, PageCount - 1);
            if (clamped == _pageIndex)
                return;

            _pageIndex = clamped;
            Refresh();
        }
    }

    /// <summary>
    /// Сколько всего записей в разделе
    /// </summary>
    private int Total => Math.Max(_allRuns.Count, _allChecks.Count);

    /// <summary>
    /// Сколько всего страниц, не меньше одной
    /// </summary>
    public int PageCount => Math.Max(1, (Total + _pageSize - 1) / _pageSize);

    /// <summary>
    /// Подпись «страница из страниц» для строки переключателя
    /// </summary>
    public string PageLabel => $"{_pageIndex + 1} из {PageCount}";

    /// <summary>
    /// Записей больше, чем влезает на самую короткую страницу: переключатель нужен
    /// </summary>
    public bool HasPaging => Total > PageSizes[0];

    /// <summary>
    /// Можно перейти на страницу назад
    /// </summary>
    private bool CanGoBack => _pageIndex > 0;

    /// <summary>
    /// Можно перейти на страницу вперёд
    /// </summary>
    private bool CanGoForward => _pageIndex < PageCount - 1;

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
    /// Пересобирает показанную страницу и сообщает окну о переключателе
    /// </summary>
    private void Refresh()
    {
        _pageIndex = Math.Clamp(_pageIndex, 0, PageCount - 1);
        Runs = [.. _allRuns.Skip(_pageIndex * _pageSize).Take(_pageSize)];
        Checks = [.. _allChecks.Skip(_pageIndex * _pageSize).Take(_pageSize)];
        Raise(nameof(PageIndex));
        Raise(nameof(PageCount));
        Raise(nameof(PageLabel));
        Raise(nameof(HasPaging));
        CommandManager.InvalidateRequerySuggested();
    }
}
