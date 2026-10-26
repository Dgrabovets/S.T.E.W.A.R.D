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
    /// Разделы журнала
    /// </summary>
    private IReadOnlyList<JournalGroupViewModel> _groups = [];

    /// <summary>
    /// Разделы журнала по идентификатору: объекты живут, пока раздел есть, чтобы свёрнутые не проигрывали анимацию при каждом обновлении
    /// </summary>
    private readonly Dictionary<string, JournalGroupViewModel> _groupById = new(StringComparer.Ordinal);

    /// <summary>
    /// Что человек раскрыл или свернул в журнале
    /// </summary>
    private readonly Dictionary<string, bool> _groupOpen = new(StringComparer.Ordinal);

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
    /// Бот сейчас разбирает рилс
    /// </summary>
    private bool _reelsBusy;

    /// <summary>
    /// Что не удалось прочитать в реестре задач
    /// </summary>
    private string _registryErrors = string.Empty;

    /// <summary>
    /// Идентификатор раздела журнала со сверками расписания
    /// </summary>
    private const string SCHEDULE_GROUP = "schedule";

    /// <summary>
    /// Создаёт модель
    /// </summary>
    public MainViewModel() : base("Проверка")
    {
        ClearJournalCommand = new RelayCommand(() => ClearJournalRequested?.Invoke(this, EventArgs.Empty));
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
    /// Просит стереть записи одного раздела журнала: параметр — идентификатор задачи или «schedule»
    /// </summary>
    public event Action<string>? ClearGroupRequested;

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
    /// Разделы журнала: у каждой задачи свой, плюс сверки расписания. Сверху тот, где запись свежее
    /// </summary>
    public IReadOnlyList<JournalGroupViewModel> Groups
    {
        get => _groups;
        private set
        {
            if (!Set(ref _groups, value))
                return;

            Raise(nameof(IsJournalEmpty));
            Raise(nameof(HasRecords));
        }
    }

    /// <summary>
    /// Журнал пуст
    /// </summary>
    public bool IsJournalEmpty => Groups.Count == 0;

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
    /// Бот сейчас разбирает рилс: на странице идей показывается сфера и пояснение
    /// </summary>
    public bool IsReelsBusy
    {
        get => _reelsBusy;
        private set => Set(ref _reelsBusy, value);
    }

    /// <summary>
    /// В журнале есть записи, их можно стереть
    /// </summary>
    public bool HasRecords => Groups.Count > 0;

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
        IsReelsBusy = rows.Any(row => row.Id == "reels" && row.Working);
        _activity.Track(rows, DateTimeOffset.UtcNow);
        Tasks = rows.Select(row => new TaskCardViewModel(row, id => RunRequested?.Invoke(id), _activity.StateOf(row), _activity.FinishedAt(row.Id))).ToArray();
        Groups = BuildJournal(snapshot, records);
        RegistryErrors = string.Join(Environment.NewLine, registryErrors);
        Ideas = ideas.Select(idea => new IdeaCardViewModel(idea, picked => IdeaApproved?.Invoke(picked), picked => IdeaRejected?.Invoke(picked), link => LinkRequested?.Invoke(link))).ToArray();

        ApplySummary(rows);
    }

    /// <summary>
    /// Раскладывает запуски по задачам, добавляет сверки расписания и сортирует разделы так, чтобы сверху был тот, где запись свежее. Раздел с последней неудачей раскрыт сам, пока человек не решил иначе
    /// </summary>
    /// <param name="snapshot">Состояние расписания с историей сверок</param>
    /// <param name="records">Запуски задач</param>
    /// <returns>Разделы журнала</returns>
    private IReadOnlyList<JournalGroupViewModel> BuildJournal(SyncSnapshot snapshot, IReadOnlyList<RunRecord> records)
    {
        var built = new List<(DateTimeOffset Last, JournalGroupViewModel Group)>();

        foreach (var byJob in records.GroupBy(record => record.JobId, StringComparer.Ordinal))
        {
            var runsOfJob = byJob.OrderByDescending(record => record.StartedAt).ToArray();
            var signature = string.Join('|', runsOfJob.Select(record => $"{record.StartedAt.Ticks}:{record.FinishedAt.Ticks}:{record.Outcome}"));
            var group = GroupOf(byJob.Key, runsOfJob[0].Title, runsOfJob[0].Outcome != RunOutcomeEnum.Ok);
            if (!group.IsCurrent(signature))
            {
                var rows = runsOfJob.Select(record => new RunRowViewModel(record, failed => FixRequested?.Invoke(failed))).ToArray();
                group.Update(signature, $"{Entries(rows.Length)} · последняя {rows[0].When}", rows[0].Tone, rows[0].Kind, rows, []);
            }

            built.Add((runsOfJob[0].StartedAt, group));
        }

        if (snapshot.Entries.Count > 0)
        {
            var signature = string.Join('|', snapshot.Entries.Select(entry => $"{entry.Time.Ticks}:{entry.Tone}:{entry.Repeat}:{entry.Detail.GetHashCode(StringComparison.Ordinal)}"));
            var group = GroupOf(SCHEDULE_GROUP, "Расписание", snapshot.Entries[0].Tone == LogToneEnum.Problem);
            if (!group.IsCurrent(signature))
            {
                var checks = snapshot.Entries.Select(entry => new ScheduleCheckViewModel(entry)).ToArray();
                var kind = checks[0].Tone switch
                {
                    PanelToneEnum.Problem => ActivityKindEnum.Shattered,
                    PanelToneEnum.Attention => ActivityKindEnum.Sweep,
                    _ => ActivityKindEnum.Sealed,
                };

                group.Update(signature, $"{Entries(checks.Length)} · последняя {checks[0].When}", checks[0].Tone, kind, [], checks);
            }

            built.Add((snapshot.Entries[0].Time, group));
        }

        var ordered = built.OrderByDescending(item => item.Last).Select(item => item.Group).ToArray();

        return ordered.SequenceEqual(_groups) ? _groups : ordered;
    }

    /// <summary>
    /// Находит раздел журнала или создаёт его
    /// </summary>
    /// <param name="id">Идентификатор задачи или «schedule»</param>
    /// <param name="title">Название раздела</param>
    /// <param name="failed">Последняя запись неудачная: тогда новый раздел раскрыт</param>
    /// <returns>Раздел, который переживает обновления</returns>
    private JournalGroupViewModel GroupOf(string id, string title, bool failed)
    {
        if (_groupById.TryGetValue(id, out var existing))
            return existing;

        var created = new JournalGroupViewModel(id, title, _groupOpen.TryGetValue(id, out var open) ? open : failed, (key, value) => _groupOpen[key] = value, key => ClearGroupRequested?.Invoke(key));
        _groupById[id] = created;

        return created;
    }

    /// <summary>
    /// Склоняет «запись» по числу
    /// </summary>
    /// <param name="count">Сколько записей</param>
    /// <returns>Число и слово в нужной форме</returns>
    private static string Entries(int count) => (count % 100 is >= 11 and <= 14 ? 0 : count % 10) switch
    {
        1 => $"{count} запись",
        2 or 3 or 4 => $"{count} записи",
        _ => $"{count} записей",
    };

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
