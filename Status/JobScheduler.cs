namespace SingularityTools.Status;

/// <summary>
/// Решает, какие задачи пора запустить. Приложение вызывает <see cref="Tick"/> каждые полминуты
/// </summary>
public sealed class JobScheduler
{
    /// <summary>
    /// Задачи, для которых условие «при запуске приложения» уже рассмотрено
    /// </summary>
    private readonly HashSet<string> _startHandled = new(StringComparer.Ordinal);

    /// <summary>
    /// Задачи из реестра
    /// </summary>
    private readonly IReadOnlyList<JobDefinition> _jobs;

    /// <summary>
    /// Хранилище статусов: из него берётся время последнего запуска
    /// </summary>
    private readonly StatusStore _store;

    /// <summary>
    /// Раннер, который запускает процессы
    /// </summary>
    private readonly JobRunner _runner;

    /// <summary>
    /// Часовой пояс для времён <c>daily</c>
    /// </summary>
    private readonly TimeZoneInfo _zone;

    /// <summary>
    /// Если задача уже запускалась позже этого срока назад, запуск «при старте» пропускается: так повторное открытие приложения не гоняет всё заново
    /// </summary>
    private readonly TimeSpan _startDedupe;

    /// <summary>
    /// Создаёт планировщик
    /// </summary>
    /// <param name="jobs">
    /// Задачи из реестра
    /// </param>
    /// <param name="store">
    /// Хранилище статусов
    /// </param>
    /// <param name="runner">
    /// Раннер
    /// </param>
    /// <param name="zone">
    /// Часовой пояс для времён <c>daily</c>
    /// </param>
    /// <param name="startDedupe">
    /// Срок, в течение которого запуск «при старте» не повторяется
    /// </param>
    public JobScheduler(IReadOnlyList<JobDefinition> jobs, StatusStore store, JobRunner runner, TimeZoneInfo zone, TimeSpan startDedupe)
    {
        _jobs = jobs;
        _store = store;
        _runner = runner;
        _zone = zone;
        _startDedupe = startDedupe;
    }

    /// <summary>
    /// Запускает задачи, которым пора. Включённые, но уже выполняющиеся пропускаются
    /// </summary>
    /// <param name="now">
    /// Текущий момент
    /// </param>
    /// <param name="cancellationToken">
    /// Останавливает запущенные процессы
    /// </param>
    /// <returns>
    /// Запуски, начатые в этот раз
    /// </returns>
    public IReadOnlyList<Task<RunRecord>> Tick(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var started = new List<Task<RunRecord>>();
        foreach (var job in _jobs.Where(job => job.Enabled && !_runner.IsRunning(job.Id)))
        {
            var status = ReadStatus(job.Id);
            if (status is { State: ProcessState.Running } && now - status.UpdatedAt < job.Timeout)
            {
                // Запуск начался до перезапуска приложения и ещё идёт: второй экземпляр не нужен
                _startHandled.Add(job.Id);
                continue;
            }

            var lastRun = status?.LastRunAt;
            string? reason = null;

            if (_startHandled.Add(job.Id) && JobSchedule.HasTrigger(job, JobTrigger.START) && (lastRun is null || now - lastRun >= _startDedupe))
                reason = JobTrigger.START;

            reason ??= JobSchedule.DueReason(job, lastRun, now, _zone);
            if (reason is not null)
                started.Add(_runner.RunAsync(job, reason, cancellationToken));
        }

        return started;
    }

    /// <summary>
    /// Запускает задачу по просьбе пользователя, даже выключенную
    /// </summary>
    /// <param name="id">
    /// Идентификатор задачи
    /// </param>
    /// <param name="cancellationToken">
    /// Останавливает процесс
    /// </param>
    /// <returns>
    /// Запуск или <see langword="null"/>, если такой задачи нет
    /// </returns>
    public Task<RunRecord>? RunNow(string id, CancellationToken cancellationToken = default)
    {
        var job = _jobs.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));
        return job is null ? null : _runner.RunAsync(job, "manual", cancellationToken);
    }

    /// <summary>
    /// Находит время ближайшего запуска по расписанию
    /// </summary>
    /// <param name="job">
    /// Задача
    /// </param>
    /// <param name="now">
    /// Текущий момент
    /// </param>
    /// <returns>
    /// Момент запуска или <see langword="null"/>, если у задачи нет условий по времени
    /// </returns>
    public DateTimeOffset? NextRun(JobDefinition job, DateTimeOffset now) =>
        job.Enabled ? JobSchedule.NextRun(job, ReadStatus(job.Id)?.LastRunAt, now, _zone) : null;

    /// <summary>
    /// Читает статус задачи и не падает от плохого идентификатора
    /// </summary>
    /// <param name="id">
    /// Идентификатор задачи
    /// </param>
    /// <returns>
    /// Статус или <see langword="null"/>, если задача ещё не запускалась
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
}
