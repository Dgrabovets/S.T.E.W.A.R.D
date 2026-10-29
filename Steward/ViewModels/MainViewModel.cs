using Steward.Homework.Models;
using Steward.Hosting;
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
    /// Вместо содержимого страницы показаны её настройки
    /// </summary>
    private bool _settingsOpen;

    /// <summary>
    /// Процессы, чья главная страница это настройки
    /// </summary>
    private IReadOnlySet<string> _settingsHome = new HashSet<string>(StringComparer.Ordinal) { TaskRows.SCHEDULE_ID };

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
    /// Напоминания о сроках сдачи
    /// </summary>
    private IReadOnlyList<DeadlineCardViewModel> _deadlines = [];

    /// <summary>
    /// Следит за выполняющимися и только что закончившимися задачами
    /// </summary>
    private readonly ActivityTracker _activity = new();

    /// <summary>
    /// Предметы и занятия с отметками
    /// </summary>
    private HomeworkFormatsViewModel? _formats;

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
    /// С чего начинаются строки вывода почты про разобранные письма
    /// </summary>
    private static readonly string[] MailPrefixes = ["Отзыв:", "Ответить:", "Инфо:", "[тест] Отзыв:", "[тест] Ответить:", "[тест] Инфо:"];

    /// <summary>
    /// Создаёт модель
    /// </summary>
    public MainViewModel() : base("Проверка")
    {
        ClearJournalCommand = new RelayCommand(() => ClearJournalRequested?.Invoke(this, EventArgs.Empty));

        NavItemViewModel Item(AppPageEnum page, string title, string? rowId) => new(page, title, rowId, () => _page, picked => Select(true, picked));
        var mail = Item(AppPageEnum.Mail, "Почта", "mail");
        var ideas = Item(AppPageEnum.Ideas, "Рилсы", "reels");
        var schedule = Item(AppPageEnum.Schedule, "Расписание", TaskRows.SCHEDULE_ID);
        var transcripts = Item(AppPageEnum.Transcripts, "Автоматизация образования", "lms");
        SystemItems = [Item(AppPageEnum.Status, "Статус", null), Item(AppPageEnum.Journal, "Журнал", null)];
        NavGroups = [new NavGroupViewModel("Моя жизнь", [mail, ideas]), new NavGroupViewModel("Учёба", [schedule, transcripts])];
    }

    /// <summary>
    /// Группы вкладок левой панели сверху вниз: «Моя жизнь» и «Учёба»
    /// </summary>
    public IReadOnlyList<NavGroupViewModel> NavGroups { get; }

    /// <summary>
    /// Системные вкладки внизу левой панели под линией: «Статус» и «Журнал»
    /// </summary>
    public IReadOnlyList<NavItemViewModel> SystemItems { get; }

    /// <summary>
    /// Все вкладки левой панели одним списком
    /// </summary>
    private IEnumerable<NavItemViewModel> NavItems => NavGroups.SelectMany(group => group.Items).Concat(SystemItems);

    /// <summary>
    /// Настройки расписания: они живут на странице «Расписание»
    /// </summary>
    public SettingsViewModel Settings { get; } = new();

    /// <summary>
    /// Предметы и занятия с отметками: страница «Расшифровки»
    /// </summary>
    public HomeworkFormatsViewModel? Formats
    {
        get => _formats;
        set => Set(ref _formats, value);
    }

    /// <summary>
    /// Строки задач последнего обновления
    /// </summary>
    private IReadOnlyList<TaskStatusRow> _rows = [];

    /// <summary>
    /// Плитки расписания: Docker, контейнер, следующая сверка и изменения за неделю
    /// </summary>
    private IReadOnlyList<StatusTileViewModel> _scheduleTiles = [];

    /// <summary>
    /// Плашки состояния настроек почты
    /// </summary>
    private IReadOnlyList<StatusChipViewModel> _mailChips = [];

    /// <summary>
    /// Плашки состояния бота рилсов и входа в Instagram
    /// </summary>
    private IReadOnlyList<StatusChipViewModel> _reelsChips = [];

    /// <summary>
    /// Плашки состояния выгрузки занятий
    /// </summary>
    private IReadOnlyList<StatusChipViewModel> _transcriptChips = [];

    /// <summary>
    /// Письма, разобранные при последних проверках почты: «Отзыв», «Ответить», «Инфо» со временем проверки
    /// </summary>
    public IReadOnlyList<MailLetterViewModel> MailLetters { get; private set; } = [];

    /// <summary>
    /// Сколько писем разобрано в каждый из последних семи дней
    /// </summary>
    public IReadOnlyList<ChartBarViewModel> MailDays { get; private set; } = [];

    /// <summary>
    /// Разобранные за неделю письма по видам: «Ответить», «Отзыв», «Инфо»
    /// </summary>
    public IReadOnlyList<ChartShareViewModel> MailKinds { get; private set; } = [];

    /// <summary>
    /// Сколько раз за неделю проверялась почта
    /// </summary>
    public int MailChecksCount { get; private set; }

    /// <summary>
    /// Когда и чем закончилась последняя проверка почты
    /// </summary>
    public string MailLastCheck { get; private set; } = "проверок ещё не было";

    /// <summary>
    /// Последняя проверка почты прошла
    /// </summary>
    public PanelToneEnum MailLastTone { get; private set; } = PanelToneEnum.Idle;

    /// <summary>
    /// Настройки почты
    /// </summary>
    public JsonSettingsViewModel? MailSettings { get; private set; }

    /// <summary>
    /// Настройки бота рилсов
    /// </summary>
    public JsonSettingsViewModel? ReelsSettings { get; private set; }

    /// <summary>
    /// Настройки выгрузки занятий
    /// </summary>
    public JsonSettingsViewModel? LmsSettings { get; private set; }

    /// <summary>
    /// Вместо содержимого страницы показаны её настройки
    /// </summary>
    public bool IsSettingsOpen
    {
        get => _settingsOpen || HomeIsSettings;
        set
        {
            if (Set(ref _settingsOpen, value))
                Raise(nameof(IsContentOpen));
        }
    }

    /// <summary>
    /// Главная страница процесса и есть его настройки: у процесса нет другого содержимого, или в реестре задано «homePage»: «settings»
    /// </summary>
    public bool HomeIsSettings => NavItems.FirstOrDefault(item => item.Page == _page)?.RowId is { } id && _settingsHome.Contains(id);

    /// <summary>
    /// Кнопка «Настройки» нужна: у страницы есть содержимое, кроме настроек
    /// </summary>
    public bool CanToggleSettings => !HomeIsSettings;

    /// <summary>
    /// Запоминает процессы, чья главная страница это настройки. Расписание такое всегда, потому что им занимается контейнер, а не задача реестра
    /// </summary>
    /// <param name="ids">Идентификаторы процессов из реестра с «homePage»: «settings»</param>
    public void SetSettingsHome(IEnumerable<string> ids)
    {
        _settingsHome = new HashSet<string>(ids, StringComparer.Ordinal) { TaskRows.SCHEDULE_ID };
        Raise(nameof(HomeIsSettings));
        Raise(nameof(CanToggleSettings));
        Raise(nameof(IsSettingsOpen));
        Raise(nameof(IsContentOpen));
    }

    /// <summary>
    /// Показано содержимое страницы, а не настройки
    /// </summary>
    public bool IsContentOpen => !IsSettingsOpen;

    /// <summary>
    /// Переключает страницу между содержимым и настройками
    /// </summary>
    public RelayCommand ToggleSettingsCommand => new(() => IsSettingsOpen = !_settingsOpen);

    /// <summary>
    /// Заводит страницы настроек процессов, если их ещё нет
    /// </summary>
    /// <param name="root">Корень репозитория</param>
    /// <param name="saved">Вызывается после записи любого из файлов</param>
    public void AttachSettings(string root, Action saved)
    {
        if (MailSettings is not null)
            return;

        MailSettings = new JsonSettingsViewModel(
            SettingsProbe.MailFile(root),
            "Пароль приложения выпускается в настройках почтового сервиса: это не пароль от ящика.",
            [
                new SettingsSectionViewModel("Учётная запись", [
                    new SettingFieldViewModel("Mail.Login", "Логин почты", ""),
                    new SettingFieldViewModel("Mail.AppPassword", "Пароль приложения", "", secret: true),
                    new SettingFieldViewModel("Mail.Host", "Сервер IMAP", ""),
                    new SettingFieldViewModel("Mail.Port", "Порт", "", number: true),
                ]),
                new SettingsSectionViewModel("Разбор писем", [
                    new SettingFieldViewModel("Mail.LookbackDays", "Сколько дней назад смотреть", "Письма старше не разбираются.", number: true),
                ]),
                new SettingsSectionViewModel("SingularityApp", [
                    new SettingFieldViewModel("Singularity.Token", "Токен", "", secret: true),
                ]),
            ],
            () => FileRequested?.Invoke("mail"));
        ReelsSettings = new JsonSettingsViewModel(
            SettingsProbe.ReelsFile(root),
            "Вход в Instagram берётся из браузера, в котором ты уже вошёл.",
            [
                new SettingsSectionViewModel("Telegram-бот", [
                    new SettingFieldViewModel("BotToken", "Токен бота", "Его выдаёт BotFather.", secret: true),
                    new SettingFieldViewModel("AllowedUserId", "Номер владельца", "Числовой идентификатор твоего аккаунта в Telegram. Бот отвечает только ему.", number: true),
                ]),
                new SettingsSectionViewModel("Instagram", [
                    new SettingFieldViewModel("CookiesFromBrowser", "Браузер для входа", "Например firefox."),
                ]),
                new SettingsSectionViewModel("Разбор идей", [
                    new SettingFieldViewModel("ClaudeCommand", "Команда Claude", ""),
                ]),
            ],
            () => FileRequested?.Invoke("reels"));
        LmsSettings = new JsonSettingsViewModel(
            SettingsProbe.LmsFile(root),
            string.Empty,
            [
                new SettingsSectionViewModel("Учётные данные LMS", [
                    new SettingFieldViewModel("Login", "Логин", ""),
                    new SettingFieldViewModel("Password", "Пароль", "", secret: true),
                    new SettingFieldViewModel("StudentName", "Ваше имя в расшифровках", "Как вас называют на занятиях. По нему в разбор попадают ваши задачи, а не поручения другим студентам."),
                ]),
                new SettingsSectionViewModel("Вход в MTS Link", [], "Войти в MTS Link", "Откроется окно браузера: войди один раз, вход сохранится.", () => MtsLoginRequested?.Invoke()),
                new SettingsSectionViewModel("Программы", [
                    new SettingFieldViewModel("MdEditor", "Программа для MD-файлов", "Например code для Visual Studio Code. Пусто: code, а если её нет, программа по умолчанию."),
                ]),
                new SettingsSectionViewModel("Выгрузка", [
                    new SettingFieldViewModel("MaxFetchPerRun", "Занятий за один запуск", "Сколько расшифровок забирать за раз.", number: true),
                ]),
            ],
            () => FileRequested?.Invoke("lms"));
        MailSettings.Saved += saved;
        ReelsSettings.Saved += saved;
        LmsSettings.Saved += saved;
        Raise(nameof(MailSettings));
        Raise(nameof(ReelsSettings));
        Raise(nameof(LmsSettings));
    }

    /// <summary>
    /// Собирает карточки задач вместе с показателями процессов
    /// </summary>
    private void BuildTasks()
    {
        IReadOnlyList<StatusTileViewModel> Metrics(string id) => id switch
        {
            TaskRows.SCHEDULE_ID => _scheduleTiles,
            "mail" => Tiles(_mailChips),
            "reels" => Tiles(_reelsChips),
            "lms" => Tiles(_transcriptChips),
            _ => [],
        };

        Tasks = _rows.Select(row => new TaskCardViewModel(row, id => RunRequested?.Invoke(id), _activity.StateOf(row), _activity.FinishedAt(row.Id), NavItems.Any(item => item.RowId == row.Id) ? OpenProcessSettings : null, Metrics(row.Id))).ToArray();
    }

    /// <summary>
    /// Превращает плашки состояния в показатели без подписи
    /// </summary>
    /// <param name="chips">Плашки</param>
    /// <returns>Показатели</returns>
    private static IReadOnlyList<StatusTileViewModel> Tiles(IReadOnlyList<StatusChipViewModel> chips) => [.. chips.Select(chip => new StatusTileViewModel(chip.Label, chip.Value, chip.Tone))];

    /// <summary>
    /// Открывает страницу процесса сразу с настройками
    /// </summary>
    /// <param name="id">Идентификатор процесса</param>
    private void OpenProcessSettings(string id)
    {
        if (NavItems.FirstOrDefault(item => item.RowId == id) is not { } target)
            return;

        Select(true, target.Page);
        IsSettingsOpen = true;
    }

    /// <summary>
    /// Обновляет показатели в карточках задач после смены плашек состояния
    /// </summary>
    private void RebuildServices() => BuildTasks();

    /// <summary>
    /// Просит открыть окно входа в MTS Link
    /// </summary>
    public event Action? MtsLoginRequested;

    /// <summary>
    /// Просит открыть файл настроек процесса: «mail», «reels» или «lms»
    /// </summary>
    public event Action<string>? FileRequested;

    /// <summary>
    /// Открывает файл настроек почты
    /// </summary>
    public RelayCommand OpenMailSettingsCommand => new(() => FileRequested?.Invoke("mail"));

    /// <summary>
    /// Открывает файл настроек бота рилсов
    /// </summary>
    public RelayCommand OpenReelsSettingsCommand => new(() => FileRequested?.Invoke("reels"));

    /// <summary>
    /// Открывает файл настроек выгрузки занятий
    /// </summary>
    public RelayCommand OpenLmsSettingsCommand => new(() => FileRequested?.Invoke("lms"));

    /// <summary>
    /// Запоминает плашки состояния настроек процессов и сообщает окну
    /// </summary>
    /// <param name="mail">Плашки почты</param>
    /// <param name="reels">Плашки бота рилсов</param>
    /// <param name="transcripts">Плашки выгрузки занятий</param>
    public void ApplyChips(IReadOnlyList<StatusChipViewModel> mail, IReadOnlyList<StatusChipViewModel> reels, IReadOnlyList<StatusChipViewModel> transcripts)
    {
        _mailChips = mail;
        _reelsChips = reels;
        _transcriptChips = transcripts;
        RebuildServices();
    }

    /// <summary>
    /// Карточка состояния почты на странице «Почта»
    /// </summary>
    public TaskCardViewModel? MailCard => Tasks.FirstOrDefault(card => card.Id == "mail");

    /// <summary>
    /// Карточка состояния расписания на странице «Расписание»
    /// </summary>
    public TaskCardViewModel? ScheduleCard => Tasks.FirstOrDefault(card => card.Id == TaskRows.SCHEDULE_ID);

    /// <summary>
    /// Карточка состояния выгрузки занятий на странице «Расшифровки»
    /// </summary>
    public TaskCardViewModel? LmsCard => Tasks.FirstOrDefault(card => card.Id == "lms");

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
    /// Просит удалить один запуск из журнала: параметр — запись
    /// </summary>
    public event Action<RunRecord>? RecordRemoveRequested;

    /// <summary>
    /// Просит удалить одну сверку расписания из журнала: параметр — запись
    /// </summary>
    public event Action<LogEntry>? CheckRemoveRequested;

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
    public bool IsMailPage
    {
        get => _page == AppPageEnum.Mail;
        set => Select(value, AppPageEnum.Mail);
    }

    /// <summary>
    /// Открыт раздел «Расписание»
    /// </summary>
    public bool IsSchedulePage
    {
        get => _page == AppPageEnum.Schedule;
        set => Select(value, AppPageEnum.Schedule);
    }

    /// <summary>
    /// Открыт раздел «Расшифровки»
    /// </summary>
    public bool IsTranscriptsPage
    {
        get => _page == AppPageEnum.Transcripts;
        set => Select(value, AppPageEnum.Transcripts);
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
    /// Напоминания о ближайших сроках сдачи на экране «Статус»
    /// </summary>
    public IReadOnlyList<DeadlineCardViewModel> Deadlines
    {
        get => _deadlines;
        private set
        {
            if (_deadlines.Count == value.Count && _deadlines.Zip(value).All(pair => pair.First.Title == pair.Second.Title && pair.First.When == pair.Second.When && pair.First.Subject == pair.Second.Subject))
                return;

            Set(ref _deadlines, value);
            Raise(nameof(HasDeadlines));
        }
    }

    /// <summary>
    /// Есть несданные работы со сроком в ближайшие дни
    /// </summary>
    public bool HasDeadlines => Deadlines.Count > 0;

    /// <summary>
    /// Показывает напоминания о сроках
    /// </summary>
    /// <param name="deadlines">Сроки по возрастанию даты</param>
    /// <param name="today">Сегодняшняя дата</param>
    public void ApplyDeadlines(IReadOnlyList<Deadline> deadlines, DateOnly today) => Deadlines = [.. deadlines.Select(deadline => new DeadlineCardViewModel(deadline, today))];

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
        _rows = rows;
        BuildTasks();
        Groups = BuildJournal(snapshot, records);
        RegistryErrors = string.Join(Environment.NewLine, registryErrors);
        Ideas = ideas.Select(idea => new IdeaCardViewModel(idea, picked => IdeaApproved?.Invoke(picked), picked => IdeaRejected?.Invoke(picked), link => LinkRequested?.Invoke(link))).ToArray();

        var weekAgo = DateTimeOffset.Now.AddDays(-7);
        var last = snapshot.Entries.Count > 0 ? snapshot.Entries.Max(entry => entry.Time) : (DateTimeOffset?)null;
        var next = last is { } seen ? seen.AddMinutes(Settings.IntervalMinutes) : (DateTimeOffset?)null;
        _scheduleTiles =
        [
            new StatusTileViewModel("Docker", snapshot.DockerAvailable ? "запущен" : "не запущен", snapshot.DockerAvailable ? PanelToneEnum.Running : PanelToneEnum.Problem),
            new StatusTileViewModel("Контейнер", snapshot.ContainerRunning ? "работает" : "остановлен", snapshot.ContainerRunning ? PanelToneEnum.Running : PanelToneEnum.Attention),
            new StatusTileViewModel("Следующая сверка", next is { } due ? (due <= DateTimeOffset.Now ? "ожидается" : "около " + due.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)) : "нет данных", PanelToneEnum.Idle),
            new StatusTileViewModel("Изменений за неделю", snapshot.Entries.Count(entry => entry.Tone == LogToneEnum.Update && entry.Time >= weekAgo).ToString(System.Globalization.CultureInfo.InvariantCulture), PanelToneEnum.Idle),
        ];
        RebuildServices();
        var mailRuns = records.Where(record => record.JobId == "mail").OrderByDescending(record => record.StartedAt).ToArray();
        var mailWeek = mailRuns.Where(record => record.StartedAt >= weekAgo).ToArray();
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var tagged = mailWeek.SelectMany(record => LogLines.Parse(record.Output ?? string.Empty)
            .Where(line => MailPrefixes.Any(prefix => line.Text.StartsWith(prefix, StringComparison.Ordinal)))
            .Select(line => (Day: record.StartedAt.ToLocalTime().Date, Kind: line.Text.Replace("[тест] ", string.Empty, StringComparison.Ordinal).Split(':')[0]))).ToArray();
        var today = DateTime.Now.Date;
        var perDay = Enumerable.Range(0, 7).Select(offset => today.AddDays(offset - 6)).Select(day => (Day: day, Count: tagged.Count(item => item.Day == day))).ToArray();
        var tallest = Math.Max(1, perDay.Max(item => item.Count));
        MailDays = [.. perDay.Select(item => new ChartBarViewModel(item.Day.ToString("ddd", new System.Globalization.CultureInfo("ru-RU")), item.Count, item.Count == 0 ? 2 : 6 + 54.0 * item.Count / tallest))];
        var kinds = new[] { ("Ответить", PanelToneEnum.Attention), ("Отзыв", PanelToneEnum.Running), ("Инфо", PanelToneEnum.Idle) };
        var most = Math.Max(1, kinds.Max(kind => tagged.Count(item => item.Kind == kind.Item1)));
        MailKinds = [.. kinds.Select(kind => new ChartShareViewModel(kind.Item1, tagged.Count(item => item.Kind == kind.Item1), (double)tagged.Count(item => item.Kind == kind.Item1) / most, kind.Item2))];
        MailChecksCount = mailWeek.Length;
        MailLastCheck = mailRuns.Length == 0 ? "проверок ещё не было" : mailRuns[0].StartedAt.ToLocalTime().ToString("dd.MM HH:mm", culture) + (mailRuns[0].Outcome == RunOutcomeEnum.Ok ? string.Empty : ", не прошла");
        MailLastTone = mailRuns.Length == 0 ? PanelToneEnum.Idle : mailRuns[0].Outcome == RunOutcomeEnum.Ok ? PanelToneEnum.Running : PanelToneEnum.Problem;
        Raise(nameof(MailDays));
        Raise(nameof(MailKinds));
        Raise(nameof(MailChecksCount));
        Raise(nameof(MailLastCheck));
        Raise(nameof(MailLastTone));
        MailLetters = [.. records.Where(record => record.JobId == "mail").OrderByDescending(record => record.StartedAt).Take(5)
            .SelectMany(record => LogLines.Parse(record.Output ?? string.Empty).Where(line => MailPrefixes.Any(prefix => line.Text.StartsWith(prefix, StringComparison.Ordinal))))
            .Take(12).Select(line => MailLetterViewModel.From(line, link => LinkRequested?.Invoke(link)))];
        Raise(nameof(MailLetters));
        Raise(nameof(MailCard));
        Raise(nameof(ScheduleCard));
        Raise(nameof(LmsCard));
        UpdateNav(ideas.Count(idea => idea.State == IdeaStateEnum.New));
        ApplySummary(rows);
    }

    /// <summary>
    /// Раскрашивает сферы вкладок по состоянию процессов и ставит числа: сколько идей ждёт решения и сколько заданий найдено
    /// </summary>
    /// <param name="newIdeas">Сколько идей ждёт решения</param>
    private void UpdateNav(int newIdeas)
    {
        foreach (var item in NavItems)
        {
            if (item.RowId is { } id && Tasks.FirstOrDefault(card => card.Id == id) is { } card)
                item.Update(card.Kind, card.Activity, card.ActivitySince, card.Tone, item.Page == AppPageEnum.Ideas ? newIdeas : 0);
            else if (item.Page == AppPageEnum.Status)
                item.Update(ActivityKindEnum.Beacon, ActivityStateEnum.None, default, Tone, 0);
            else if (item.Page == AppPageEnum.Ideas)
                item.Update(ActivityKindEnum.Dormant, ActivityStateEnum.None, default, PanelToneEnum.Idle, newIdeas);
        }
    }

    /// <summary>
    /// Раскладывает запуски по задачам, добавляет сверки расписания и держит разделы на постоянных местах, по идентификатору, чтобы они не прыгали после удаления записи. Раздел с последней неудачей раскрыт сам, пока человек не решил иначе
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
                var rows = CollapseRepeats(runsOfJob).Select(same => new RunRowViewModel(same[0], failed => FixRequested?.Invoke(failed), removed => RecordRemoveRequested?.Invoke(removed), same)).ToArray();
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
                var checks = snapshot.Entries.Select(entry => new ScheduleCheckViewModel(entry, removed => CheckRemoveRequested?.Invoke(removed))).ToArray();
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

        var ordered = built.OrderBy(item => item.Group.Id, StringComparer.Ordinal).Select(item => item.Group).ToArray();

        return ordered.SequenceEqual(_groups) ? _groups : ordered;
    }

    /// <summary>
    /// Склеивает подряд идущие запуски с тем же итогом и тем же выводом, чтобы журнал не заполнялся одинаковыми записями
    /// </summary>
    /// <param name="runs">Запуски одной задачи от новых к старым</param>
    /// <returns>Серии запусков: в каждой запуски от новых к старым, одиночный запуск даёт серию из одного</returns>
    private static IReadOnlyList<IReadOnlyList<RunRecord>> CollapseRepeats(IReadOnlyList<RunRecord> runs)
    {
        var series = new List<List<RunRecord>>();
        foreach (var run in runs)
        {
            if (series.Count > 0 && RunRowViewModel.SameKey(series[^1][0]) == RunRowViewModel.SameKey(run))
                series[^1].Add(run);
            else
                series.Add([run]);
        }

        return series;
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
    public void ApplyHomework(IReadOnlyList<HomeworkRow> rows)
    {
        Homework = [.. rows.Select(row => new HomeworkCardViewModel(row))];
        if (NavItems.FirstOrDefault(item => item.Page == AppPageEnum.Homework) is { } item)
            item.Update(item.Kind, item.Activity, item.ActivitySince, item.Tone, rows.Count);
    }

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
        IsSettingsOpen = false;
        Raise(nameof(Page));
        Raise(nameof(HomeIsSettings));
        Raise(nameof(CanToggleSettings));
        Raise(nameof(IsSettingsOpen));
        Raise(nameof(IsContentOpen));
        Raise(nameof(IsStatusPage));
        Raise(nameof(IsIdeasPage));
        Raise(nameof(IsHomeworkPage));
        Raise(nameof(IsJournalPage));
        Raise(nameof(IsMailPage));
        Raise(nameof(IsSchedulePage));
        Raise(nameof(IsTranscriptsPage));
        foreach (var item in NavItems)
            item.RefreshSelection();
    }

    #endregion Updating
}
