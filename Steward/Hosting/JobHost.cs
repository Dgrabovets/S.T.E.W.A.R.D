using System.IO;
using System.Windows.Threading;
using Steward.Status;

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
    /// <param name="id">
    /// Идентификатор задачи
    /// </param>
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
    /// <returns>
    /// Строки по порядку реестра
    /// </returns>
    public IReadOnlyList<JobRow> Rows()
    {
        var now = DateTimeOffset.Now;
        return _jobs.Select(job => JobRowBuilder.Build(job, ReadStatus(job.Id), _scheduler.NextRun(job, now), now)).ToArray();
    }

    /// <summary>
    /// Читает последние записи журнала запусков
    /// </summary>
    /// <returns>
    /// Записи от новых к старым
    /// </returns>
    public IReadOnlyList<RunRecord> Records() => _runner.History.Load(RECORD_COUNT);

    /// <summary>
    /// Читает файл статуса и не падает от повреждённого файла
    /// </summary>
    /// <param name="id">
    /// Идентификатор задачи
    /// </param>
    /// <returns>
    /// Статус или <see langword="null"/>
    /// </returns>
    private ProcessStatus? ReadStatus(string id)
    {
        try
        {
            return _store.Read(id);
        }
        catch (ArgumentException)
        {
            return null;
        }
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
