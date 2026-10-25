using Steward.Controls;
using Steward.Models;
using Steward.Mvvm;
using Steward.Monitoring.Models;
using Steward.Status.Rows;
using Steward.Status.Running;

namespace Steward.ViewModels;

/// <summary>
/// Состояние главного окна: раздел, задачи, журнал и общий итог
/// </summary>
public sealed class MainViewModel : PanelViewModel
{
    /// <summary>
    /// Текущий раздел
    /// </summary>
    private AppPageEnum _page = AppPageEnum.Status;

    /// <summary>
    /// Карточки задач
    /// </summary>
    private IReadOnlyList<TaskCardViewModel> _tasks = [];

    /// <summary>
    /// Строки журнала
    /// </summary>
    private IReadOnlyList<RunRowViewModel> _records = [];

    /// <summary>
    /// Задачи, которые выполнялись при прошлом обновлении
    /// </summary>
    private HashSet<string> _working = [];

    /// <summary>
    /// Когда закончились задачи, которым ещё показывается галочка
    /// </summary>
    private readonly Dictionary<string, DateTimeOffset> _finished = [];

    /// <summary>
    /// Что не удалось прочитать в реестре задач
    /// </summary>
    private string _registryErrors = string.Empty;

    /// <summary>
    /// Создаёт модель
    /// </summary>
    public MainViewModel() : base("Проверка")
    {
        ClearJournalCommand = new RelayCommand(() => ClearJournalRequested?.Invoke(this, EventArgs.Empty));
        OpenFormatsCommand = new RelayCommand(() => FormatsRequested?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>
    /// Просит запустить задачу: параметр — идентификатор
    /// </summary>
    public event Action<string>? RunRequested;

    /// <summary>
    /// Просит отдать неудавшийся запуск Claude на починку: параметр — запись журнала
    /// </summary>
    public event Action<RunRecord>? FixRequested;

    /// <summary>
    /// Просит стереть журнал запусков
    /// </summary>
    public event EventHandler? ClearJournalRequested;

    /// <summary>
    /// Просит открыть окно выбора форматов ДЗ
    /// </summary>
    public event EventHandler? FormatsRequested;

    /// <summary>
    /// Стирает журнал запусков
    /// </summary>
    public RelayCommand ClearJournalCommand { get; }

    /// <summary>
    /// Открывает окно выбора форматов ДЗ
    /// </summary>
    public RelayCommand OpenFormatsCommand { get; }

    #region State

    /// <summary>
    /// Открыт раздел «Статус»
    /// </summary>
    public bool IsStatusPage
    {
        get => _page == AppPageEnum.Status;
        set => Select(value, AppPageEnum.Status);
    }

    /// <summary>
    /// Открыт раздел «Идеи»
    /// </summary>
    public bool IsIdeasPage
    {
        get => _page == AppPageEnum.Ideas;
        set => Select(value, AppPageEnum.Ideas);
    }

    /// <summary>
    /// Открыт раздел «ДЗ»
    /// </summary>
    public bool IsHomeworkPage
    {
        get => _page == AppPageEnum.Homework;
        set => Select(value, AppPageEnum.Homework);
    }

    /// <summary>
    /// Открыт раздел «Журнал»
    /// </summary>
    public bool IsJournalPage
    {
        get => _page == AppPageEnum.Journal;
        set => Select(value, AppPageEnum.Journal);
    }

    /// <summary>
    /// Текущий раздел
    /// </summary>
    public AppPageEnum Page
    {
        get => _page;
        set => Select(true, value);
    }

    /// <summary>
    /// Карточки задач
    /// </summary>
    public IReadOnlyList<TaskCardViewModel> Tasks
    {
        get => _tasks;
        private set => Set(ref _tasks, value);
    }

    /// <summary>
    /// Строки журнала запусков от новых к старым
    /// </summary>
    public IReadOnlyList<RunRowViewModel> Records
    {
        get => _records;
        private set
        {
            if (!Set(ref _records, value))
                return;

            Raise(nameof(IsJournalEmpty));
            Raise(nameof(HasRecords));
        }
    }

    /// <summary>
    /// Журнал пуст
    /// </summary>
    public bool IsJournalEmpty => Records.Count == 0;

    /// <summary>
    /// В журнале есть записи, их можно стереть
    /// </summary>
    public bool HasRecords => Records.Count > 0;

    /// <summary>
    /// Что не удалось прочитать в реестре задач
    /// </summary>
    public string RegistryErrors
    {
        get => _registryErrors;
        private set
        {
            if (!Set(ref _registryErrors, value))
                return;

            Raise(nameof(HasRegistryErrors));
        }
    }

    /// <summary>
    /// В реестре есть ошибки, их нужно показать
    /// </summary>
    public bool HasRegistryErrors => RegistryErrors.Length > 0;

    #endregion State

    #region Updating

    /// <summary>
    /// Показывает свежие данные
    /// </summary>
    /// <param name="snapshot">Состояние контейнера расписания</param>
    /// <param name="jobs">Строки задач из реестра</param>
    /// <param name="records">Последние запуски</param>
    /// <param name="registryErrors">Ошибки чтения реестра</param>
    public void Apply(SyncSnapshot snapshot, IReadOnlyList<JobRow> jobs, IReadOnlyList<RunRecord> records, IReadOnlyList<string> registryErrors)
    {
        var rows = TaskRows.Build(snapshot, jobs);
        TrackFinished(rows, DateTimeOffset.UtcNow);
        Tasks = rows.Select(row => new TaskCardViewModel(row, id => RunRequested?.Invoke(id), ActivityOf(row), _finished.GetValueOrDefault(row.Id))).ToArray();
        Records = records.Select(record => new RunRowViewModel(record, failed => FixRequested?.Invoke(failed))).ToArray();
        RegistryErrors = string.Join(Environment.NewLine, registryErrors);

        ApplySummary(rows);
    }

    /// <summary>
    /// Замечает задачи, которые перестали выполняться с прошлого обновления, и запоминает, когда это случилось. Сбойные не отмечаются: им галочка не положена
    /// </summary>
    /// <param name="rows">Строки задач</param>
    /// <param name="now">Текущий момент</param>
    private void TrackFinished(IReadOnlyList<TaskStatusRow> rows, DateTimeOffset now)
    {
        foreach (var row in rows.Where(row => !row.Working && row.Tone != PanelToneEnum.Problem && _working.Contains(row.Id)))
            _finished[row.Id] = now;

        foreach (var id in _finished.Where(pair => (now - pair.Value).TotalSeconds >= ActivityOrb.DONE_SHOW_SECONDS).Select(pair => pair.Key).ToList())
            _finished.Remove(id);

        _working = [.. rows.Where(row => row.Working).Select(row => row.Id)];
    }

    /// <summary>
    /// Что показывать у задачи: вращение, галочку или обычную точку
    /// </summary>
    /// <param name="row">Строка задачи</param>
    /// <returns>Состояние индикатора</returns>
    private ActivityStateEnum ActivityOf(TaskStatusRow row) =>
        row.Working ? ActivityStateEnum.Working : _finished.ContainsKey(row.Id) ? ActivityStateEnum.Done : ActivityStateEnum.None;

    /// <summary>
    /// Переключает раздел и сообщает окну обо всех зависящих свойствах
    /// </summary>
    /// <param name="selected">
    /// <see langword="true"/>, если раздел выбран. Снятие выбора игнорируется: его делает выбор соседнего раздела
    /// </param>
    /// <param name="page">Раздел</param>
    private void Select(bool selected, AppPageEnum page)
    {
        if (!selected || _page == page)
            return;

        _page = page;
        Raise(nameof(Page));
        Raise(nameof(IsStatusPage));
        Raise(nameof(IsIdeasPage));
        Raise(nameof(IsHomeworkPage));
        Raise(nameof(IsJournalPage));
    }

    #endregion Updating
}
