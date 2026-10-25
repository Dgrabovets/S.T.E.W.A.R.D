using System.IO;
using System.Windows.Threading;
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
    /// Читает идеи, которые ждут решения
    /// </summary>
    /// <returns>Новые идеи от свежих к старым</returns>
    public IReadOnlyList<Idea> Ideas() => [.. _ideas.LoadAll().Where(idea => idea.State == IdeaStateEnum.New)];

    /// <summary>
    /// Путь к файлу идеи
    /// </summary>
    /// <param name="id">Идентификатор идеи</param>
    /// <returns>Полный путь к файлу</returns>
    public string IdeaPath(string id) => _ideas.PathFor(id);

    /// <summary>
    /// Записывает решение по идее и обновляет число ожидающих в статусе задачи «reels», не дожидаясь следующего запуска бота
    /// </summary>
    /// <param name="id">Идентификатор идеи</param>
    /// <param name="state">Решение</param>
    public void SetIdeaState(string id, IdeaStateEnum state)
    {
        if (!_ideas.SetState(id, state) || _store.TryRead(REELS_ID) is not { State: ProcessStateEnum.Ok or ProcessStateEnum.NeedsAttention } status)
        {
            Changed?.Invoke(this, EventArgs.Empty);

            return;
        }

        var awaiting = Ideas().Count;
        _store.Write(status with { Awaiting = awaiting, State = awaiting > 0 ? ProcessStateEnum.NeedsAttention : ProcessStateEnum.Ok });
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
