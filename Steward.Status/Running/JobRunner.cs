using System.ComponentModel;
using System.Diagnostics;
using Steward.Status.Common;
using Steward.Status.Registry;
using Steward.Status.State;

namespace Steward.Status.Running;

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
    /// Сколько ждать остаток вывода после выхода процесса
    /// </summary>
    private static readonly TimeSpan OUTPUT_DRAIN = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Сколько последних строк вывода хранить в журнале
    /// </summary>
    private const int OUTPUT_LINES = 40;

    /// <summary>
    /// Наибольшая длина хранимого вывода в символах
    /// </summary>
    private const int OUTPUT_LIMIT = 4000;

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
    /// <param name="store">Хранилище статусов</param>
    /// <param name="dataDirectory">Папка данных</param>
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
    /// <param name="id">Идентификатор задачи</param>
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
    /// <param name="job">Задача</param>
    /// <param name="reason">Почему запускается: start, daily, interval или manual</param>
    /// <param name="cancellationToken">Останавливает процесс, например при закрытии приложения</param>
    /// <returns>Запись о запуске. Задача не бросает исключений: ошибка попадает в запись и в статус</returns>
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
    /// Выполняет один запуск и в любом случае освобождает задачу для следующего
    /// </summary>
    /// <param name="job">Задача</param>
    /// <param name="reason">Причина запуска</param>
    /// <param name="cancellationToken">Останавливает процесс</param>
    /// <returns>Запись о запуске</returns>
    private async Task<RunRecord> ExecuteAsync(JobDefinition job, string reason, CancellationToken cancellationToken)
    {
        RunRecord record;
        try
        {
            record = await RunCoreAsync(job, reason, cancellationToken);
        }
        finally
        {
            lock (_gate)
                _running.Remove(job.Id);
        }

        Finished?.Invoke(this, record);
        return record;
    }

    /// <summary>
    /// Выполняет один запуск от начала до записи итога
    /// </summary>
    /// <param name="job">Задача</param>
    /// <param name="reason">Причина запуска</param>
    /// <param name="cancellationToken">Останавливает процесс</param>
    /// <returns>Запись о запуске</returns>
    private async Task<RunRecord> RunCoreAsync(JobDefinition job, string reason, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.Now;
        var result = new ProcessResult(RunOutcomeEnum.NotStarted, null, null);

        try
        {
            _store.Report(job.Id, job.DisplayTitle, ProcessStateEnum.Running, started, $"Запуск: {reason}", null, success: false);
            Started?.Invoke(this, job.Id);

            result = await StartAndWaitAsync(job, cancellationToken);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            result = result with { Detail = exception.Message };
        }

        var finished = DateTimeOffset.Now;
        try
        {
            Finish(job, started, finished, result);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            result = result with { Detail = result.Detail ?? exception.Message };
        }

        // Итоговый статус уже записан: либо самим процессом, либо раннером. Из него берутся сводка и полный текст ошибки
        var status = _store.TryRead(job.Id);
        var detail = result.Outcome == RunOutcomeEnum.Ok ? null : status?.Error ?? result.Detail;
        var record = new RunRecord(job.Id, job.DisplayTitle, reason, started, finished, result.ExitCode, result.Outcome, detail, status?.Summary, result.Output);
        History.Append(record);

        return record;
    }

    /// <summary>
    /// Запускает процесс и ждёт его конца, но не дольше таймаута задачи
    /// </summary>
    /// <param name="job">Задача</param>
    /// <param name="cancellationToken">Останавливает процесс</param>
    /// <returns>Итог, код завершения, пояснение к ошибке и хвост вывода</returns>
    private async Task<ProcessResult> StartAndWaitAsync(JobDefinition job, CancellationToken cancellationToken)
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
                return new ProcessResult(RunOutcomeEnum.NotStarted, null, exception.Message);
            }

            if (process is null)
                return new ProcessResult(RunOutcomeEnum.NotStarted, null, "Процесс не создан");

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
                    ? new ProcessResult(RunOutcomeEnum.Failed, null, "Остановлена при закрытии приложения")
                    : new ProcessResult(RunOutcomeEnum.TimedOut, null, $"Не завершилась за {job.TimeoutMinutes:0.##} мин, процесс остановлен");
            }

            // Внук процесса может держать трубу открытой и после выхода самого процесса: ждать вывод бесконечно нельзя
            var drained = Task.WhenAll(output, error);
            await Task.WhenAny(drained, Task.Delay(OUTPUT_DRAIN));

            var tail = drained.IsCompletedSuccessfully ? LastLine(error.Result) : null;
            var text = drained.IsCompletedSuccessfully ? OutputTail(output.Result + "\n" + error.Result) : null;

            return process.ExitCode == 0
                ? new ProcessResult(RunOutcomeEnum.Ok, 0, null, text)
                : new ProcessResult(RunOutcomeEnum.Failed, process.ExitCode, tail is null ? $"Код завершения {process.ExitCode}" : $"Код завершения {process.ExitCode}: {tail}", text);
        }
        finally
        {
            process?.Dispose();
        }
    }

    /// <summary>
    /// Записывает итог запуска, если процесс не рассказал о себе сам
    /// </summary>
    /// <param name="job">Задача</param>
    /// <param name="started">Когда запуск начался</param>
    /// <param name="finished">Когда запуск закончился</param>
    /// <param name="result">Итог процесса</param>
    private void Finish(JobDefinition job, DateTimeOffset started, DateTimeOffset finished, ProcessResult result)
    {
        var current = _store.TryRead(job.Id);
        var selfReported = current is { } own && own.State != ProcessStateEnum.Running && own.UpdatedAt > started;

        if (result.Outcome == RunOutcomeEnum.Ok)
        {
            if (!selfReported)
                _store.Report(job.Id, job.DisplayTitle, ProcessStateEnum.Ok, finished, "Завершено без ошибок", null, success: true);
            else if (current!.LastRunAt is null || current.LastRunAt < started)
                _store.Write(current with { LastRunAt = finished });

            return;
        }

        var reportedFailure = selfReported && current!.State == ProcessStateEnum.Failed;
        if (!reportedFailure)
            _store.Report(job.Id, job.DisplayTitle, ProcessStateEnum.Failed, finished, null, result.Detail ?? $"Код завершения {result.ExitCode}", success: false);
        else if (current!.LastRunAt is null || current.LastRunAt < started)
            _store.Write(current with { LastRunAt = finished });
    }

    #endregion Execution

    #region Helpers

    /// <summary>
    /// Выбирает рабочий каталог: заданный, иначе каталог программы
    /// </summary>
    /// <param name="job">Задача</param>
    /// <returns>Каталог или пустая строка, если подойдёт текущий</returns>
    private static string WorkingDirectoryOf(JobDefinition job)
    {
        if (!string.IsNullOrWhiteSpace(job.WorkingDirectory))
            return job.WorkingDirectory;

        var directory = Path.IsPathRooted(job.Command) ? Path.GetDirectoryName(job.Command) : null;
        return directory is not null && Directory.Exists(directory) ? directory : string.Empty;
    }

    /// <summary>
    /// Оставляет конец вывода процесса: последние строки, но не больше лимита символов
    /// </summary>
    /// <param name="text">Весь вывод: стандартный поток и поток ошибок</param>
    /// <returns>
    /// Последние непустые строки или <see langword="null"/>, если вывода не было
    /// </returns>
    private static string? OutputTail(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).TakeLast(OUTPUT_LINES);
        var tail = string.Join('\n', lines);

        return tail.Length == 0 ? null : tail.Length <= OUTPUT_LIMIT ? tail : "…" + tail[^OUTPUT_LIMIT..];
    }

    /// <summary>
    /// Берёт последнюю непустую строку вывода об ошибках
    /// </summary>
    /// <param name="text">Весь вывод</param>
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
    /// <param name="process">Процесс</param>
    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            // ignore
        }
    }

    #endregion Helpers
}
