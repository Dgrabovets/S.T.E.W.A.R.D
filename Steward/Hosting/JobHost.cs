using System.IO;
using System.Net.Http;
using System.Windows.Threading;
using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Homework.Storage;
using Steward.Ideas.Models;
using Steward.Ideas.Storage;
using Steward.Status.Common;
using Steward.Status.Registry;
using Steward.Status.Rows;
using Steward.Status.Running;
using Steward.Status.Scheduling;
using Steward.Status.State;

namespace Steward.Hosting;


    /// <summary>
/// Запускает задачи из <c>config/jobs.json</c> по их условиям и отдаёт панелям готовые строки статуса
/// </summary>
public sealed class JobHost : IDisposable
{
    /// <summary>
    /// Как часто планировщик проверяет, не пора ли что-то запустить
    /// </summary>
    private static readonly TimeSpan TICK_INTERVAL = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Если задача уже запускалась позже этого срока назад, запуск «при старте» пропускается: повторное открытие приложения не гоняет всё заново
    /// </summary>
    private static readonly TimeSpan START_DEDUPE = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Сколько записей журнала показывать
    /// </summary>
    private const int RECORD_COUNT = 100;

    /// <summary>
    /// Идентификатор задачи выгрузки занятий и расшифровок из LMS в config/jobs.json
    /// </summary>
    private const string LMS_ID = "lms";

    /// <summary>
    /// Идентификатор задачи бота для рилсов в config/jobs.json
    /// </summary>
    private const string REELS_ID = "reels";

    /// <summary>
    /// Поток окна: в него переправляются события из рабочих потоков
    /// </summary>
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    /// <summary>
    /// Повторная проверка условий запуска
    /// </summary>
    private readonly DispatcherTimer _timer = new() { Interval = TICK_INTERVAL };

    /// <summary>
    /// Папка файлов статусов
    /// </summary>
    private readonly StatusStore _store;

    /// <summary>
    /// Папка домашних заданий: занятия и выбранные форматы
    /// </summary>
    private readonly HomeworkStore _homework;

    /// <summary>
    /// Методические указания и образцы предметов
    /// </summary>
    private readonly GuideStore _guides;

    /// <summary>
    /// Клиент для скачивания материалов по ссылкам
    /// </summary>
    private static readonly HttpClient GuideHttp = new() { Timeout = TimeSpan.FromSeconds(60) };



    /// <summary>
    /// Папка идей из рилсов
    /// </summary>
    private readonly IdeaStore _ideas;

    /// <summary>
    /// Запускает процессы и ведёт журнал
    /// </summary>
    private readonly JobRunner _runner;

    /// <summary>
    /// Решает, что пора запустить
    /// </summary>
    private readonly JobScheduler _scheduler;

    /// <summary>
    /// Задачи из реестра
    /// </summary>
    private readonly IReadOnlyList<JobDefinition> _jobs;

    /// <summary>
    /// Читает реестр и готовит раннер. Запуск начинается в <see cref="Start"/>
    /// </summary>
    public JobHost()
    {
        var dataDirectory = DataLocation.DataDirectory();
        _store = new StatusStore(Path.Combine(dataDirectory, "status"));
        _homework = new HomeworkStore(Path.Combine(dataDirectory, "homework"));
        _guides = new GuideStore(Path.Combine(dataDirectory, "homework"));
        _ideas = new IdeaStore(Path.Combine(dataDirectory, "ideas"));

        var registry = JobRegistry.Load(DataLocation.JobsFile(), DataLocation.FindRepoRoot());
        _jobs = registry.Jobs;
        RegistryErrors = registry.Errors;

        _runner = new JobRunner(_store, dataDirectory);
        _scheduler = new JobScheduler(_jobs, _store, _runner, TimeZoneInfo.Local, START_DEDUPE);
        _runner.Started += (_, _) => RaiseChanged();
        _runner.Finished += (_, _) => RaiseChanged();
        _timer.Tick += (_, _) => Tick();
    }

    /// <summary>
    /// Что-то изменилось: запуск начался или закончился, либо прошла проверка условий. Событие приходит в потоке окна
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Что не удалось прочитать в <c>jobs.json</c>
    /// </summary>
    public IReadOnlyList<string> RegistryErrors { get; }

    /// <summary>
    /// Папка данных, из которой читаются статусы
    /// </summary>
    public string StatusDirectory => _store.Directory;

    #region Control

    /// <summary>
    /// Запускает задачи с условием «при старте» и включает проверку по таймеру
    /// </summary>
    public void Start()
    {
        Tick();
        _timer.Start();
    }

    /// <summary>
    /// Запускает задачу по просьбе пользователя
    /// </summary>
    /// <param name="id">Идентификатор задачи</param>
    /// <returns>
    /// <see langword="true"/>, если такая задача есть
    /// </returns>
    public bool RunNow(string id) => _scheduler.RunNow(id) is not null;

    /// <summary>
    /// Перестаёт запускать новые задачи. Уже запущенные процессы не трогает: оборванная проверка почты могла бы создать задачи в Singularity и не запомнить письма, а при следующем запуске создать их заново. Зависшие по-прежнему останавливает таймаут
    /// </summary>
    public void Dispose() => _timer.Stop();

    #endregion Control

    #region Reading

    /// <summary>
    /// Собирает строки статуса всех задач реестра
    /// </summary>
    /// <returns>Строки по порядку реестра</returns>
    public IReadOnlyList<JobRow> Rows()
    {
        var now = DateTimeOffset.Now;
        return _jobs.Select(job => JobRowBuilder.Build(job, _store.TryRead(job.Id), _scheduler.NextRun(job, now), now)).ToArray();
    }

    /// <summary>
    /// Читает последние записи журнала запусков
    /// </summary>
    /// <returns>Записи от новых к старым</returns>
    public IReadOnlyList<RunRecord> Records() => _runner.History.Load(RECORD_COUNT);

    /// <summary>
    /// Читает занятия и формат ДЗ, выбранный для каждого
    /// </summary>
    /// <returns>Занятия от свежих к старым с их форматами</returns>
    public IReadOnlyList<(Lesson Lesson, HomeworkFormatEnum Format)> Lessons() =>
        [.. _homework.LoadLessons().Where(lesson => _homework.StateFor(lesson.Id) != LessonStateEnum.Skip).Select(lesson => (lesson, _homework.SubjectFormatFor(lesson.Title)))];

    /// <summary>
    /// Читает найденные домашние задания
    /// </summary>
    /// <returns>Задания по занятиям от свежих к старым</returns>
    public IReadOnlyList<HomeworkRow> HomeworkRows() => _homework.LoadRows();

    /// <summary>
    /// Читает текст расшифровки занятия
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Текст или пустая строка</returns>
    public string ReadTranscript(string lessonId) => _homework.ReadTranscript(lessonId);

    /// <summary>
    /// Методические указания и образцы предмета
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <returns>Материалы предмета</returns>
    public IReadOnlyList<GuideEntry> Guides(string subject) => _guides.ForSubject(subject);

    /// <summary>
    /// Копирует выбранные файлы в папку материалов предмета
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <param name="kind">Указания или образцы</param>
    /// <param name="paths">Файлы, которые выбрал пользователь</param>
    /// <returns>Текст ошибки или пустая строка</returns>
    public string AddGuideFiles(string subject, GuideKindEnum kind, IEnumerable<string> paths)
    {
        var errors = new List<string>();
        foreach (var path in paths)
        {
            try
            {
                _guides.AddFile(subject, kind, path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                errors.Add($"{Path.GetFileName(path)}: {exception.Message}");
            }
        }

        return string.Join("; ", errors);
    }

    /// <summary>
    /// Запоминает ссылку на файл в интернете и скачивает файл. Ссылки Google Диска и Документов превращаются в прямые
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <param name="kind">Указания или образцы</param>
    /// <param name="url">Адрес в интернете</param>
    /// <returns>Пояснение, если что-то не вышло, иначе пустая строка</returns>
    public async Task<string> AddGuideLinkAsync(string subject, GuideKindEnum kind, string url)
    {
        try
        {
            var entry = _guides.AddReference(subject, kind, url);

            return entry.Url.Length > 0 ? (await _guides.DownloadAsync(entry, GuideHttp, CancellationToken.None)).Note : string.Empty;
        }
        catch (ArgumentException exception)
        {
            return exception.Message;
        }
    }

    /// <summary>
    /// Запоминает папку по пути на диске: оригиналы остаются на месте
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <param name="kind">Указания или образцы</param>
    /// <param name="path">Путь к папке</param>
    /// <returns>Пояснение, если что-то не вышло, иначе пустая строка</returns>
    public string AddGuideFolder(string subject, GuideKindEnum kind, string path)
    {
        try
        {
            _guides.AddReference(subject, kind, path);

            return string.Empty;
        }
        catch (ArgumentException exception)
        {
            return exception.Message;
        }
    }

    /// <summary>
    /// Включает или выключает материал
    /// </summary>
    /// <param name="id">Ключ записи</param>
    /// <param name="enabled">Учитывать материал</param>
    public void SetGuideEnabled(string id, bool enabled) => _guides.SetEnabled(id, enabled);

    /// <summary>
    /// Убирает материал предмета
    /// </summary>
    /// <param name="id">Ключ записи</param>
    public void RemoveGuide(string id) => _guides.Remove(id);

    /// <summary>
    /// Читает подробный разбор занятия
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Разбор или <see langword="null"/>, если его ещё нет</returns>
    public LessonAnalysis? ReadAnalysis(string lessonId) => _homework.ReadAnalysis(lessonId);

    /// <summary>
    /// Находит готовые файлы заданий занятия
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Пути к файлам</returns>
    public IReadOnlyList<string> DraftFiles(string lessonId) => _homework.DraftFiles(lessonId);

    /// <summary>
    /// Читает работы из LMS для счётчиков на плитках предметов
    /// </summary>
    /// <returns>Задания и тесты</returns>
    public IReadOnlyList<ActivityInfo> Activities() => _homework.LoadActivities();

    /// <summary>
    /// Процессы, главная страница которых это настройки
    /// </summary>
    /// <returns>Идентификаторы процессов с «homePage»: «settings»</returns>
    public IReadOnlyList<string> SettingsHomeIds() => [.. _jobs.Where(job => string.Equals(job.HomePage, "settings", StringComparison.OrdinalIgnoreCase)).Select(job => job.Id)];

    /// <summary>
    /// Проверяет, лежит ли расшифровка занятия
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <returns>
    /// <see langword="true"/>, если файл расшифровки есть
    /// </returns>
    public bool HasTranscript(Lesson lesson) => File.Exists(_homework.TranscriptPath(lesson.Id));

    /// <summary>
    /// Просит Claude в фоне найти в расшифровке домашние задания, а затем выполнить каждое в выбранном формате и положить готовые файлы в папку занятия. Чат при этом не создаётся
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="request">Формат, дополнительное указание и материалы, которые нужно учесть</param>
    /// <param name="cancellationToken">Отменяет запуск</param>
    /// <returns>Сколько заданий найдено и текст ошибки, если не получилось</returns>
    public async Task<(int Count, string Error)> PrepareHomeworkAsync(Lesson lesson, PrepareRequest request, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_homework.ItemsPath(lesson.Id))!);

        var prompt = HomeworkExtractionPrompt.Build(lesson, _homework.TranscriptPath(lesson.Id), _homework.ItemsPath(lesson.Id));
        var error = await ClaudeAgent.RunAsync(prompt, _homework.Root, cancellationToken);
        if (error.Length > 0)
            return (0, error);

        if (_homework.ReadItems(lesson.Id) is not { } items)
            return (0, "Claude не записал файл заданий");

        var format = request.Format;
        if (items.Count == 0 || format == HomeworkFormatEnum.Skip)
            return (items.Count, string.Empty);

        var (_, draftError) = await DraftBuilder.BuildAsync(_homework, lesson, items, format, ClaudeAgent.RunAsync, cancellationToken, _guides.Materials(lesson.Title, request.GuideIds), request.Extra);
        if (draftError.Length > 0)
            return (items.Count, draftError);

        RunNow(LMS_ID);

        return (items.Count, string.Empty);
    }

    /// <summary>
    /// Запоминает формат ДЗ для занятия
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="format">Выбранный формат</param>
    public void SetHomeworkFormat(string lessonId, HomeworkFormatEnum format) => _homework.SetFormat(lessonId, format);

    /// <summary>
    /// Запоминает формат заданий по умолчанию для предмета
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <param name="format">Выбранный формат</param>
    public void SetSubjectFormat(string subject, HomeworkFormatEnum format) => _homework.SetSubjectFormat(subject, format);

    /// <summary>
    /// Заказывает расшифровку занятия независимо от отметки предмета и сразу запускает выгрузку
    /// </summary>
    /// <param name="lesson">Занятие, расшифровку которого нужно забрать</param>
    public void RequestTranscript(Lesson lesson)
    {
        _homework.SetState(lesson.Id, LessonStateEnum.Fetch);
        RunNow(LMS_ID);
    }

    /// <summary>
    /// Читает отметку предмета
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <returns>Что делать с занятиями предмета: расшифровывать или нет</returns>
    public LessonStateEnum SubjectState(string subject) => _homework.SubjectStateFor(subject);

    /// <summary>
    /// Запоминает отметку предмета
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <param name="state">Выбранная отметка</param>
    public void SetSubjectState(string subject, LessonStateEnum state) => _homework.SetSubjectState(subject, state);

    /// <summary>
    /// Читает идеи, которые остались в списке: новые и взятые в работу. Убирает их из списка только человек
    /// </summary>
    /// <returns>Идеи от свежих к старым</returns>
    public IReadOnlyList<Idea> Ideas() => [.. _ideas.LoadAll().Where(idea => idea.State is IdeaStateEnum.New or IdeaStateEnum.Approved)];

    /// <summary>
    /// Путь к файлу идеи
    /// </summary>
    /// <param name="id">Идентификатор идеи</param>
    /// <returns>Полный путь к файлу</returns>
    public string IdeaPath(string id) => _ideas.PathFor(id);

    /// <summary>
    /// Удаляет идею по просьбе человека и обновляет число ожидающих в статусе так же, как после решения
    /// </summary>
    /// <param name="id">Идентификатор идеи</param>
    public void DeleteIdea(string id)
    {
        _ideas.Delete(id);
        RefreshAwaiting();
    }

    /// <summary>
    /// Записывает решение по идее и обновляет число ожидающих в статусе задачи «reels», не дожидаясь следующего запуска бота
    /// </summary>
    /// <param name="id">Идентификатор идеи</param>
    /// <param name="state">Решение</param>
    public void SetIdeaState(string id, IdeaStateEnum state)
    {
        _ideas.SetState(id, state);
        RefreshAwaiting();
    }

    /// <summary>
    /// Пересчитывает идеи без решения, записывает число в статус задачи «reels» и сообщает панелям
    /// </summary>
    private void RefreshAwaiting()
    {
        if (_store.TryRead(REELS_ID) is { State: ProcessStateEnum.Ok or ProcessStateEnum.NeedsAttention } status)
        {
            var awaiting = _ideas.LoadAll().Count(idea => idea.State == IdeaStateEnum.New);
            _store.Write(status with { Awaiting = awaiting, State = awaiting > 0 ? ProcessStateEnum.NeedsAttention : ProcessStateEnum.Ok });
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Стирает журнал запусков и сообщает панелям
    /// </summary>
    public void ClearRecords()
    {
        _runner.History.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Удаляет один запуск из журнала и сообщает панелям
    /// </summary>
    /// <param name="record">Запись, которую убирают</param>
    public void RemoveRecord(RunRecord record)
    {
        _runner.History.Remove(record);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Стирает запуски одной задачи и сообщает панелям
    /// </summary>
    /// <param name="jobId">Идентификатор задачи</param>
    public void ClearRecords(string jobId)
    {
        _runner.History.Clear(jobId);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    #endregion Reading

    #region Events

    /// <summary>
    /// Проверяет условия запуска и сообщает панелям
    /// </summary>
    private void Tick()
    {
        _scheduler.Tick(DateTimeOffset.Now);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Переправляет событие раннера в поток окна. Если приложение уже закрывается, молчит
    /// </summary>
    private void RaiseChanged()
    {
        if (_dispatcher.HasShutdownStarted)
            return;

        _dispatcher.BeginInvoke(() => Changed?.Invoke(this, EventArgs.Empty));
    }

    #endregion Events
}
