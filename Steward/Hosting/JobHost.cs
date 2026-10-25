using System.IO;
using System.Windows.Threading;
using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Homework.Storage;
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
        [.. _homework.LoadLessons().Select(lesson => (lesson, _homework.FormatFor(lesson.Id)))];

    /// <summary>
    /// Проверяет, лежит ли расшифровка занятия
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <returns>
    /// <see langword="true"/>, если файл расшифровки есть
    /// </returns>
    public bool HasTranscript(Lesson lesson) => File.Exists(_homework.TranscriptPath(lesson.Id));

    /// <summary>
    /// Просит Claude в фоне найти в расшифровке домашние задания и ждёт, пока он запишет их в файл. Чат при этом не создаётся
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="cancellationToken">Отменяет запуск</param>
    /// <returns>Сколько заданий найдено и текст ошибки, если не получилось</returns>
    public async Task<(int Count, string Error)> PrepareHomeworkAsync(Lesson lesson, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_homework.ItemsPath(lesson.Id))!);

        var prompt = HomeworkExtractionPrompt.Build(lesson, _homework.TranscriptPath(lesson.Id), _homework.ItemsPath(lesson.Id));
        var error = await ClaudeAgent.RunAsync(prompt, _homework.Root, cancellationToken);
        if (error.Length > 0)
            return (0, error);

        return _homework.ReadItems(lesson.Id) is { } items ? (items.Count, string.Empty) : (0, "Claude не записал файл заданий");
    }

    /// <summary>
    /// Запоминает формат ДЗ для занятия
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="format">Выбранный формат</param>
    public void SetHomeworkFormat(string lessonId, HomeworkFormatEnum format) => _homework.SetFormat(lessonId, format);

    /// <summary>
    /// Стирает журнал запусков и сообщает панелям
    /// </summary>
    public void ClearRecords()
    {
        _runner.History.Clear();
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
