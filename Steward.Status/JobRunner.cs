using System.ComponentModel;
using System.Diagnostics;

namespace Steward.Status;

/// <summary>
/// Запускает задачи как отдельные процессы и ведёт их статус. Одна задача выполняется не более чем в одном экземпляре
/// </summary>
public sealed class JobRunner
{
    /// <summary>
    /// Длина хвоста ошибки из вывода программы
    /// </summary>
    private const int DETAIL_LIMIT = 200;

    /// <summary>
    /// Хранилище статусов
    /// </summary>
    private readonly StatusStore _store;

    /// <summary>
    /// Папка данных: её получает каждый процесс, и в ней лежит его временная папка
    /// </summary>
    private readonly string _dataDirectory;

    /// <summary>
    /// Защищает таблицу выполняющихся задач
    /// </summary>
    private readonly object _gate = new();

    /// <summary>
    /// Выполняющиеся задачи по идентификатору
    /// </summary>
    private readonly Dictionary<string, Task<RunRecord>> _running = new(StringComparer.Ordinal);

    /// <summary>
    /// Создаёт раннер
    /// </summary>
    /// <param name="store">
    /// Хранилище статусов
    /// </param>
    /// <param name="dataDirectory">
    /// Папка данных
    /// </param>
    public JobRunner(StatusStore store, string dataDirectory)
    {
        _store = store;
        _dataDirectory = dataDirectory;
        History = new RunHistory(dataDirectory);
    }

    /// <summary>
    /// Запуск начался: параметр — идентификатор задачи
    /// </summary>
    public event EventHandler<string>? Started;

    /// <summary>
    /// Запуск закончился: параметр — запись журнала. Событие приходит из рабочего потока
    /// </summary>
    public event EventHandler<RunRecord>? Finished;

    /// <summary>
    /// Журнал запусков
    /// </summary>
    public RunHistory History { get; }

    #region Running

    /// <summary>
    /// Сообщает, выполняется ли задача сейчас
    /// </summary>
    /// <param name="id">
    /// Идентификатор задачи
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если запуск ещё не закончился
    /// </returns>
    public bool IsRunning(string id)
    {
        lock (_gate)
            return _running.ContainsKey(id);
    }

    /// <summary>
    /// Запускает задачу. Если она уже выполняется, возвращает тот же запуск, а не начинает второй
    /// </summary>
    /// <param name="job">
    /// Задача
    /// </param>
    /// <param name="reason">
    /// Почему запускается: start, daily, interval или manual
    /// </param>
    /// <param name="cancellationToken">
    /// Останавливает процесс, например при закрытии приложения
    /// </param>
    /// <returns>
    /// Запись о запуске. Задача не бросает исключений: ошибка попадает в запись и в статус
    /// </returns>
    public Task<RunRecord> RunAsync(JobDefinition job, string reason, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_running.TryGetValue(job.Id, out var existing))
                return existing;

            var task = Task.Run(() => ExecuteAsync(job, reason, cancellationToken), CancellationToken.None);
            _running[job.Id] = task;
            return task;
        }
    }

    #endregion Running

    #region Execution

    /// <summary>
    /// Выполняет один запуск от начала до записи итога
    /// </summary>
    /// <param name="job">
    /// Задача
    /// </param>
    /// <param name="reason">
    /// Причина запуска
    /// </param>
    /// <param name="cancellationToken">
    /// Останавливает процесс
    /// </param>
    /// <returns>
    /// Запись о запуске
    /// </returns>
    private async Task<RunRecord> ExecuteAsync(JobDefinition job, string reason, CancellationToken cancellationToken)
    {
        var title = string.IsNullOrWhiteSpace(job.Title) ? job.Id : job.Title;
        var started = DateTimeOffset.Now;
        ProcessStatus? previous = null;
        int? exitCode = null;
        var outcome = RunOutcome.NotStarted;
        string? detail = null;

        try
        {
            previous = SafeRead(job.Id);
            Save(job, title, previous, ProcessState.Running, started, $"Запуск: {reason}", null, success: false);
            Started?.Invoke(this, job.Id);

            (outcome, exitCode, detail) = await StartAndWaitAsync(job, cancellationToken);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            detail = exception.Message;
        }

        var finished = DateTimeOffset.Now;
        try
        {
            Finish(job, title, previous, started, finished, outcome, exitCode, detail);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            detail ??= exception.Message;
        }

        var record = new RunRecord(job.Id, title, reason, started, finished, exitCode, outcome, detail);
        History.Append(record);
        lock (_gate)
            _running.Remove(job.Id);

        Finished?.Invoke(this, record);
        return record;
    }

    /// <summary>
    /// Запускает процесс и ждёт его конца, но не дольше таймаута задачи
    /// </summary>
    /// <param name="job">
    /// Задача
    /// </param>
    /// <param name="cancellationToken">
    /// Останавливает процесс
    /// </param>
    /// <returns>
    /// Итог, код завершения и пояснение к ошибке
    /// </returns>
    private async Task<(RunOutcome Outcome, int? ExitCode, string? Detail)> StartAndWaitAsync(JobDefinition job, CancellationToken cancellationToken)
    {
        var temp = Path.Combine(_dataDirectory, "tmp");
        Directory.CreateDirectory(temp);

        var startInfo = new ProcessStartInfo(job.Command, job.Arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = WorkingDirectoryOf(job),
        };
        startInfo.Environment[DataLocation.DATA_ENVIRONMENT_VARIABLE] = _dataDirectory;
        startInfo.Environment["TEMP"] = temp;
        startInfo.Environment["TMP"] = temp;

        Process? process = null;
        try
        {
            try
            {
                process = Process.Start(startInfo);
            }
            catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or DirectoryNotFoundException)
            {
                return (RunOutcome.NotStarted, null, exception.Message);
            }

            if (process is null)
                return (RunOutcome.NotStarted, null, "Процесс не создан");

            var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var error = process.StandardError.ReadToEndAsync(CancellationToken.None);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(job.Timeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                Kill(process);
                return cancellationToken.IsCancellationRequested
                    ? (RunOutcome.Failed, null, "Остановлена при закрытии приложения")
                    : (RunOutcome.TimedOut, null, $"Не завершилась за {job.TimeoutMinutes:0.##} мин, процесс остановлен");
            }

            await output;
            var tail = LastLine(await error);
            return process.ExitCode == 0
                ? (RunOutcome.Ok, 0, null)
                : (RunOutcome.Failed, process.ExitCode, tail is null ? $"Код завершения {process.ExitCode}" : $"Код завершения {process.ExitCode}: {tail}");
        }
        finally
        {
            process?.Dispose();
        }
    }

    /// <summary>
    /// Записывает итог запуска, если процесс не рассказал о себе сам
    /// </summary>
    /// <param name="job">
    /// Задача
    /// </param>
    /// <param name="title">
    /// Название задачи
    /// </param>
    /// <param name="previous">
    /// Статус до запуска
    /// </param>
    /// <param name="started">
    /// Когда запуск начался
    /// </param>
    /// <param name="finished">
    /// Когда запуск закончился
    /// </param>
    /// <param name="outcome">
    /// Итог процесса
    /// </param>
    /// <param name="exitCode">
    /// Код завершения
    /// </param>
    /// <param name="detail">
    /// Пояснение к ошибке
    /// </param>
    private void Finish(JobDefinition job, string title, ProcessStatus? previous, DateTimeOffset started, DateTimeOffset finished, RunOutcome outcome, int? exitCode, string? detail)
    {
        var current = SafeRead(job.Id);
        var selfReported = current is { } own && own.State != ProcessState.Running && own.UpdatedAt > started;

        if (outcome == RunOutcome.Ok)
        {
            if (!selfReported)
                Save(job, title, previous, ProcessState.Ok, finished, "Завершено без ошибок", null, success: true);
            else if (current!.LastRunAt is null || current.LastRunAt < started)
                _store.Write(current with { LastRunAt = finished });

            return;
        }

        var reportedFailure = selfReported && current!.State == ProcessState.Failed;
        if (!reportedFailure)
            Save(job, title, previous, ProcessState.Failed, finished, null, detail ?? $"Код завершения {exitCode}", success: false);
        else if (current!.LastRunAt is null || current.LastRunAt < started)
            _store.Write(current with { LastRunAt = finished });
    }

    /// <summary>
    /// Записывает статус от имени раннера
    /// </summary>
    /// <param name="job">
    /// Задача
    /// </param>
    /// <param name="title">
    /// Название задачи
    /// </param>
    /// <param name="previous">
    /// Статус до запуска: из него берутся прошлый успех и число ожидающих
    /// </param>
    /// <param name="state">
    /// Новое состояние
    /// </param>
    /// <param name="at">
    /// Момент записи
    /// </param>
    /// <param name="summary">
    /// Сводка
    /// </param>
    /// <param name="error">
    /// Текст ошибки
    /// </param>
    /// <param name="success">
    /// <see langword="true"/>, если запуск удался и нужно обновить время успеха
    /// </param>
    private void Save(JobDefinition job, string title, ProcessStatus? previous, ProcessState state, DateTimeOffset at, string? summary, string? error, bool success)
    {
        _store.Write(new ProcessStatus
        {
            Id = job.Id,
            Title = title,
            State = state,
            UpdatedAt = at,
            LastRunAt = state == ProcessState.Running ? previous?.LastRunAt : at,
            LastSuccessAt = success ? at : previous?.LastSuccessAt,
            NextRunAt = previous?.NextRunAt,
            Summary = summary,
            Error = error,
            Awaiting = previous?.Awaiting ?? 0,
        });
    }

    #endregion Execution

    #region Helpers

    /// <summary>
    /// Читает статус и не падает, если идентификатор или файл плохие
    /// </summary>
    /// <param name="id">
    /// Идентификатор задачи
    /// </param>
    /// <returns>
    /// Статус или <see langword="null"/>
    /// </returns>
    private ProcessStatus? SafeRead(string id)
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

    /// <summary>
    /// Выбирает рабочий каталог: заданный, иначе каталог программы
    /// </summary>
    /// <param name="job">
    /// Задача
    /// </param>
    /// <returns>
    /// Каталог или пустая строка, если подойдёт текущий
    /// </returns>
    private static string WorkingDirectoryOf(JobDefinition job)
    {
        if (!string.IsNullOrWhiteSpace(job.WorkingDirectory))
            return job.WorkingDirectory;

        var directory = Path.IsPathRooted(job.Command) ? Path.GetDirectoryName(job.Command) : null;
        return directory is not null && Directory.Exists(directory) ? directory : string.Empty;
    }

    /// <summary>
    /// Берёт последнюю непустую строку вывода об ошибках
    /// </summary>
    /// <param name="text">
    /// Весь вывод
    /// </param>
    /// <returns>
    /// Строка не длиннее лимита или <see langword="null"/>
    /// </returns>
    private static string? LastLine(string text)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        return line is null ? null : line.Length <= DETAIL_LIMIT ? line : line[..DETAIL_LIMIT] + "…";
    }

    /// <summary>
    /// Останавливает процесс вместе с дочерними
    /// </summary>
    /// <param name="process">
    /// Процесс
    /// </param>
    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
        }
    }

    #endregion Helpers
}
