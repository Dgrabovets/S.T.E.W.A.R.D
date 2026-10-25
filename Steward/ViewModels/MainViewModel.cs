using System.Globalization;
using Steward.Status;
using Steward.Models;
using Steward.Monitoring.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Раздел главного окна
/// </summary>
public enum AppPage
{
    /// <summary>
    /// Статус всех задач
    /// </summary>
    Status,

    /// <summary>
    /// Идеи, которые ждут решения
    /// </summary>
    Ideas,

    /// <summary>
    /// Подготовленные черновики домашних заданий
    /// </summary>
    Homework,

    /// <summary>
    /// Журнал запусков
    /// </summary>
    Journal,
}

/// <summary>
/// Карточка задачи в разделе «Статус»
/// </summary>
public sealed class TaskCardViewModel
{
    /// <summary>
    /// Создаёт карточку
    /// </summary>
    /// <param name="row">Строка задачи</param>
    /// <param name="run">Действие кнопки «Запустить»</param>
    public TaskCardViewModel(TaskStatusRow row, Action<string> run)
    {
        Name = row.Name;
        Headline = row.Headline;
        Detail = row.Detail;
        Tone = row.Tone;
        Muted = row.Muted;
        CanRun = row.CanRun;
        RunCommand = new RelayCommand(() => run(row.Id), () => row.CanRun);
    }

    /// <summary>
    /// Название задачи
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Короткий статус
    /// </summary>
    public string Headline { get; }

    /// <summary>
    /// Подробности
    /// </summary>
    public string Detail { get; }

    /// <summary>
    /// Цвет точки
    /// </summary>
    public PanelTone Tone { get; }

    /// <summary>
    /// Задача выключена
    /// </summary>
    public bool Muted { get; }

    /// <summary>
    /// У карточки есть кнопка запуска
    /// </summary>
    public bool CanRun { get; }

    /// <summary>
    /// Запускает задачу сейчас
    /// </summary>
    public RelayCommand RunCommand { get; }
}

/// <summary>
/// Строка журнала запусков
/// </summary>
public sealed class RunRowViewModel
{
    /// <summary>
    /// Создаёт строку по записи журнала
    /// </summary>
    /// <param name="record">Запись о запуске</param>
    public RunRowViewModel(RunRecord record)
    {
        When = record.StartedAt.ToLocalTime().ToString("dd.MM HH:mm:ss", CultureInfo.InvariantCulture);
        Title = record.Title;
        Reason = record.Reason switch
        {
            JobTrigger.START => "при запуске приложения",
            JobTrigger.DAILY => "по расписанию",
            JobTrigger.INTERVAL => "по интервалу",
            "manual" => "вручную",
            _ => record.Reason,
        };
        Outcome = record.Outcome switch
        {
            RunOutcome.Ok => "Успешно",
            RunOutcome.Failed => "Ошибка",
            RunOutcome.TimedOut => "Не уложилась во время",
            _ => "Не запустилась",
        };
        Detail = record.Detail ?? string.Empty;
        Tone = record.Outcome == RunOutcome.Ok ? PanelTone.Running : PanelTone.Problem;
    }

    /// <summary>
    /// Когда запущена
    /// </summary>
    public string When { get; }

    /// <summary>
    /// Название задачи
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Почему запущена
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Чем закончилась
    /// </summary>
    public string Outcome { get; }

    /// <summary>
    /// Пояснение к ошибке
    /// </summary>
    public string Detail { get; }

    /// <summary>
    /// Есть что показать под названием: у успешных запусков пояснения нет
    /// </summary>
    public bool HasDetail => Detail.Length > 0;

    /// <summary>
    /// Цвет точки
    /// </summary>
    public PanelTone Tone { get; }
}

/// <summary>
/// Состояние главного окна: раздел, задачи, журнал и общий итог
/// </summary>
public sealed class MainViewModel : Bindable
{
    /// <summary>
    /// Текущий раздел
    /// </summary>
    private AppPage _page = AppPage.Status;

    /// <summary>
    /// Карточки задач
    /// </summary>
    private IReadOnlyList<TaskCardViewModel> _tasks = [];

    /// <summary>
    /// Строки журнала
    /// </summary>
    private IReadOnlyList<RunRowViewModel> _records = [];

    /// <summary>
    /// Что не удалось прочитать в реестре задач
    /// </summary>
    private string _registryErrors = string.Empty;

    /// <summary>
    /// Строка под заголовком
    /// </summary>
    private string _subtitle = "Проверяю задачи";

    /// <summary>
    /// Текст плашки
    /// </summary>
    private string _statusLabel = "Проверка";

    /// <summary>
    /// Цвет плашки
    /// </summary>
    private PanelTone _tone = PanelTone.Idle;

    /// <summary>
    /// Создаёт модель
    /// </summary>
    public MainViewModel()
    {
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>
    /// Просит запустить задачу: параметр — идентификатор
    /// </summary>
    public event Action<string>? RunRequested;

    /// <summary>
    /// Просит закрыть приложение
    /// </summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    /// Закрывает приложение целиком
    /// </summary>
    public RelayCommand ExitCommand { get; }

    #region State

    /// <summary>
    /// Открыт раздел «Статус»
    /// </summary>
    public bool IsStatusPage
    {
        get => _page == AppPage.Status;
        set => Select(value, AppPage.Status);
    }

    /// <summary>
    /// Открыт раздел «Идеи»
    /// </summary>
    public bool IsIdeasPage
    {
        get => _page == AppPage.Ideas;
        set => Select(value, AppPage.Ideas);
    }

    /// <summary>
    /// Открыт раздел «ДЗ»
    /// </summary>
    public bool IsHomeworkPage
    {
        get => _page == AppPage.Homework;
        set => Select(value, AppPage.Homework);
    }

    /// <summary>
    /// Открыт раздел «Журнал»
    /// </summary>
    public bool IsJournalPage
    {
        get => _page == AppPage.Journal;
        set => Select(value, AppPage.Journal);
    }

    /// <summary>
    /// Текущий раздел
    /// </summary>
    public AppPage Page
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
        }
    }

    /// <summary>
    /// Журнал пуст
    /// </summary>
    public bool IsJournalEmpty => Records.Count == 0;

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

    /// <summary>
    /// Строка под заголовком окна
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
    public PanelTone Tone
    {
        get => _tone;
        private set => Set(ref _tone, value);
    }

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
        Tasks = rows.Select(row => new TaskCardViewModel(row, id => RunRequested?.Invoke(id))).ToArray();
        Records = records.Select(record => new RunRowViewModel(record)).ToArray();
        RegistryErrors = string.Join(Environment.NewLine, registryErrors);

        var summary = TaskSummary.Of(rows);
        Tone = summary.Tone;
        StatusLabel = summary.Label;
        Subtitle = summary.Subtitle;
    }

    /// <summary>
    /// Переключает раздел и сообщает окну обо всех зависящих свойствах
    /// </summary>
    /// <param name="selected">
    /// <see langword="true"/>, если раздел выбран. Снятие выбора игнорируется: его делает выбор соседнего раздела
    /// </param>
    /// <param name="page">Раздел</param>
    private void Select(bool selected, AppPage page)
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
