using Steward.Homework.Models;
using Steward.Ideas.Models;
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
    /// Карточки найденных домашних заданий
    /// </summary>
    private IReadOnlyList<HomeworkCardViewModel> _homework = [];

    /// <summary>
    /// Следит за выполняющимися и только что закончившимися задачами
    /// </summary>
    private readonly ActivityTracker _activity = new();

    /// <summary>
    /// Карточки идей, которые ждут решения
    /// </summary>
    private IReadOnlyList<IdeaCardViewModel> _ideas = [];

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
    /// Решено делать идею: параметр — идея
    /// </summary>
    public event Action<Idea>? IdeaApproved;

    /// <summary>
    /// Решено не делать идею: параметр — идея
    /// </summary>
    public event Action<Idea>? IdeaRejected;

    /// <summary>
    /// Просит открыть ссылку в браузере: параметр — адрес
    /// </summary>
    public event Action<string>? LinkRequested;

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
    /// Карточки найденных домашних заданий
    /// </summary>
    public IReadOnlyList<HomeworkCardViewModel> Homework
    {
        get => _homework;
        private set
        {
            if (!Set(ref _homework, value))
                return;

            Raise(nameof(IsHomeworkEmpty));
        }
    }

    /// <summary>
    /// Найденных заданий пока нет
    /// </summary>
    public bool IsHomeworkEmpty => Homework.Count == 0;

    /// <summary>
    /// Карточки идей, которые ждут решения
    /// </summary>
    public IReadOnlyList<IdeaCardViewModel> Ideas
    {
        get => _ideas;
        private set
        {
            if (!Set(ref _ideas, value))
                return;

            Raise(nameof(IsIdeasEmpty));
        }
    }

    /// <summary>
    /// Идей, которые ждут решения, нет
    /// </summary>
    public bool IsIdeasEmpty => Ideas.Count == 0;

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
    /// <param name="ideas">Идеи, которые ждут решения</param>
    public void Apply(SyncSnapshot snapshot, IReadOnlyList<JobRow> jobs, IReadOnlyList<RunRecord> records, IReadOnlyList<string> registryErrors, IReadOnlyList<Idea> ideas)
    {
        var rows = TaskRows.Build(snapshot, jobs);
        _activity.Track(rows, DateTimeOffset.UtcNow);
        Tasks = rows.Select(row => new TaskCardViewModel(row, id => RunRequested?.Invoke(id), _activity.StateOf(row), _activity.FinishedAt(row.Id))).ToArray();
        Records = records.Select(record => new RunRowViewModel(record, failed => FixRequested?.Invoke(failed))).ToArray();
        RegistryErrors = string.Join(Environment.NewLine, registryErrors);
        Ideas = ideas.Select(idea => new IdeaCardViewModel(idea, picked => IdeaApproved?.Invoke(picked), picked => IdeaRejected?.Invoke(picked), link => LinkRequested?.Invoke(link))).ToArray();

        ApplySummary(rows);
    }

    /// <summary>
    /// Показывает найденные домашние задания
    /// </summary>
    /// <param name="rows">Задания с занятиями от свежих к старым</param>
    public void ApplyHomework(IReadOnlyList<HomeworkRow> rows) => Homework = [.. rows.Select(row => new HomeworkCardViewModel(row))];

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
