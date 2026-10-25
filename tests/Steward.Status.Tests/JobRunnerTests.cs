using System.Diagnostics;
using Steward.Status.Running;
using Steward.Status.Scheduling;
using Steward.Status.State;

namespace Steward.Status.Tests;

/// <summary>
/// Проверяет раннер и планировщик на настоящих дочерних процессах. Запускаются только безобидные команды
/// </summary>
public class JobRunnerTests
{
    /// <summary>
    /// Собирает раннер, хранилище и папку данных внутри временной папки теста
    /// </summary>
    /// <param name="dir">Временная папка теста</param>
    /// <returns>Раннер и хранилище</returns>
    private static (JobRunner Runner, StatusStore Store) Create(TempDir dir)
    {
        var store = new StatusStore(dir.StatusPath);
        return (new JobRunner(store, dir.Path), store);
    }

    /// <summary>
    /// Программа без собственного статуса: раннер записывает «ок» и время успеха
    /// </summary>
    [Fact]
    public async Task A_clean_exit_without_its_own_status_is_recorded_as_ok()
    {
        using var dir = new TempDir();
        var (runner, store) = Create(dir);

        var record = await runner.RunAsync(Samples.Job("quiet") with { Arguments = "/c exit 0" }, "manual");

        Assert.Equal(RunOutcomeEnum.Ok, record.Outcome);
        var status = store.Read("quiet")!;
        Assert.Equal(ProcessStateEnum.Ok, status.State);
        Assert.NotNull(status.LastSuccessAt);
        Assert.NotNull(status.LastRunAt);
        Assert.Equal("quiet", runner.History.Load(5).Single().JobId);
    }

    /// <summary>
    /// Ненулевой код и текст из потока ошибок попадают в статус
    /// </summary>
    [Fact]
    public async Task A_failing_exit_is_recorded_with_the_code_and_the_last_error_line()
    {
        using var dir = new TempDir();
        var (runner, store) = Create(dir);
        var before = new ProcessStatus { Id = "flaky", State = ProcessStateEnum.Ok, UpdatedAt = Samples.Winter(2026, 10, 24, 9), LastSuccessAt = Samples.Winter(2026, 10, 24, 9) };
        store.Write(before);

        var record = await runner.RunAsync(Samples.Job("flaky") with { Arguments = "/c \"echo boom 1>&2 & exit 3\"" }, "interval");

        Assert.Equal((RunOutcomeEnum.Failed, 3), (record.Outcome, record.ExitCode));
        var status = store.Read("flaky")!;
        Assert.Equal(ProcessStateEnum.Failed, status.State);
        Assert.Equal("Код завершения 3: boom", status.Error);
        Assert.Equal(before.LastSuccessAt, status.LastSuccessAt);
    }

    /// <summary>
    /// Программа сама пишет статус «ждёт решения», получает папку данных и временную папку через окружение, раннер её статус не затирает
    /// </summary>
    [Fact]
    public async Task A_job_that_reports_itself_keeps_its_status_and_gets_the_environment()
    {
        using var dir = new TempDir();
        var (runner, store) = Create(dir);
        var script = Path.Combine(dir.Path, "selfish.ps1");
        File.WriteAllText(script, """
            $dir = Join-Path $env:STEWARD_DATA 'status'
            New-Item -ItemType Directory -Force $dir | Out-Null
            $json = '{"id":"selfish","title":"Self","state":"needsAttention","updatedAt":"' + (Get-Date).ToString('o') + '","summary":"two items","awaiting":2}'
            Set-Content -Path (Join-Path $dir 'selfish.json') -Value $json -Encoding UTF8
            Set-Content -Path (Join-Path $env:STEWARD_DATA 'temp-was.txt') -Value $env:TEMP
            """);

        var job = Samples.Job("selfish") with { Command = "powershell.exe", Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"" };

        var record = await runner.RunAsync(job, "manual");

        Assert.Equal(RunOutcomeEnum.Ok, record.Outcome);
        var status = store.Read("selfish")!;
        Assert.Equal((ProcessStateEnum.NeedsAttention, 2, "two items"), (status.State, status.Awaiting, status.Summary));
        Assert.NotNull(status.LastRunAt);
        Assert.Equal(Path.Combine(dir.Path, "tmp"), File.ReadAllText(Path.Combine(dir.Path, "temp-was.txt")).Trim());
    }

    /// <summary>
    /// Хвост вывода программы и сводка попадают в запись журнала, а ошибка берётся из итогового статуса
    /// </summary>
    [Fact]
    public async Task The_record_keeps_the_output_tail_the_summary_and_the_full_error()
    {
        using var dir = new TempDir();
        var (runner, _) = Create(dir);

        var record = await runner.RunAsync(Samples.Job("noisy") with { Arguments = "/c \"echo first line & echo second line & echo boom 1>&2 & exit 2\"" }, "manual");

        Assert.Equal("Код завершения 2: boom", record.Detail);
        Assert.Contains("first line", record.Output, StringComparison.Ordinal);
        Assert.Contains("boom", record.Output, StringComparison.Ordinal);
        Assert.Null(record.Summary);
        Assert.Equal("noisy", runner.History.Load(1).Single().JobId);
        Assert.Contains("second line", runner.History.Load(1).Single().Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Успешный запуск без вывода не хранит пустой вывод, но сохраняет сводку раннера
    /// </summary>
    [Fact]
    public async Task A_silent_successful_run_has_no_output_and_the_runner_summary()
    {
        using var dir = new TempDir();
        var (runner, _) = Create(dir);

        var record = await runner.RunAsync(Samples.Job("silent") with { Arguments = "/c exit 0" }, "manual");

        Assert.Null(record.Output);
        Assert.Null(record.Detail);
        Assert.Equal("Завершено без ошибок", record.Summary);
    }

    /// <summary>
    /// Если программа сама записала ошибку, в журнал идёт её полный текст, а не короткий код завершения
    /// </summary>
    [Fact]
    public async Task A_self_reported_error_replaces_the_exit_code_text_in_the_record()
    {
        using var dir = new TempDir();
        var (runner, _) = Create(dir);
        var script = Path.Combine(dir.Path, "self-error.ps1");
        File.WriteAllText(script, """
            $dir = Join-Path $env:STEWARD_DATA 'status'
            New-Item -ItemType Directory -Force $dir | Out-Null
            $json = '{"id":"self-error","title":"Self","state":"failed","updatedAt":"' + (Get-Date).ToString('o') + '","error":"Token is not set in Singularity:Token"}'
            Set-Content -Path (Join-Path $dir 'self-error.json') -Value $json -Encoding UTF8
            exit 1
            """);

        var record = await runner.RunAsync(Samples.Job("self-error") with { Command = "powershell.exe", Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"" }, "manual");

        Assert.Equal(RunOutcomeEnum.Failed, record.Outcome);
        Assert.Equal("Token is not set in Singularity:Token", record.Detail);
    }
    /// <summary>
    /// Зависшая программа останавливается по таймауту, статус становится «не удалось»
    /// </summary>
    [Fact]
    public async Task A_hung_job_is_killed_at_its_timeout()
    {
        using var dir = new TempDir();
        var (runner, store) = Create(dir);
        var job = Samples.Job("hang") with { Command = "powershell.exe", Arguments = "-NoProfile -Command Start-Sleep -Seconds 60", TimeoutMinutes = 0.05 };
        var watch = Stopwatch.StartNew();

        var record = await runner.RunAsync(job, "manual");

        Assert.Equal(RunOutcomeEnum.TimedOut, record.Outcome);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(30), "процесс должен быть остановлен по таймауту, а не дожит до конца");
        Assert.Equal(ProcessStateEnum.Failed, store.Read("hang")!.State);
        Assert.Contains("Не завершилась", store.Read("hang")!.Error, StringComparison.Ordinal);
    }

    /// <summary>
    /// Несуществующая программа не роняет раннер и даёт понятный статус
    /// </summary>
    [Fact]
    public async Task A_missing_program_is_reported_not_thrown()
    {
        using var dir = new TempDir();
        var (runner, store) = Create(dir);
        var job = Samples.Job("ghost") with { Command = Path.Combine(dir.Path, "no-such-program.exe") };

        var record = await runner.RunAsync(job, "manual");

        Assert.Equal(RunOutcomeEnum.NotStarted, record.Outcome);
        Assert.Equal(ProcessStateEnum.Failed, store.Read("ghost")!.State);
    }

    /// <summary>
    /// Два запроса на запуск одной задачи сливаются в один процесс
    /// </summary>
    [Fact]
    public async Task Overlapping_runs_of_one_job_are_merged()
    {
        using var dir = new TempDir();
        var (runner, _) = Create(dir);
        var job = Samples.Job("slow") with { Command = "powershell.exe", Arguments = "-NoProfile -Command Start-Sleep -Seconds 2" };

        var first = runner.RunAsync(job, "start");
        var second = runner.RunAsync(job, "manual");

        Assert.True(runner.IsRunning("slow"));
        await Task.WhenAll(first, second);

        Assert.Same(first, second);
        Assert.False(runner.IsRunning("slow"));
        Assert.Single(runner.History.Load(10));
    }

    /// <summary>
    /// Запуск «при старте» срабатывает один раз за жизнь планировщика, а после перезапуска приложения пропускается, если задача недавно уже работала
    /// </summary>
    [Fact]
    public async Task The_start_trigger_runs_once_and_is_deduplicated_across_restarts()
    {
        using var dir = new TempDir();
        var (runner, store) = Create(dir);
        var job = Samples.Job("boot", Samples.OnStart);
        var now = DateTimeOffset.Now;

        var first = new JobScheduler([job], store, runner, TimeZoneInfo.Local, TimeSpan.FromMinutes(10));
        var started = first.Tick(now);

        await Task.WhenAll(started);
        var again = first.Tick(now.AddSeconds(30));

        Assert.True(started.Count == 1, "при старте должен начаться ровно один запуск");
        Assert.Empty(again);
        Assert.Equal("start", (await started[0]).Reason);

        Assert.Empty(new JobScheduler([job], store, runner, TimeZoneInfo.Local, TimeSpan.FromMinutes(10)).Tick(now.AddMinutes(2)));

        var afterAWhile = new JobScheduler([job], store, runner, TimeZoneInfo.Local, TimeSpan.FromMinutes(10)).Tick(now.AddMinutes(30));
        await Task.WhenAll(afterAWhile);
        Assert.True(afterAWhile.Count == 1, "спустя срок дедупликации запуск должен повториться");
    }

    /// <summary>
    /// Запуск, начатый до перезапуска приложения и ещё не просроченный, не дублируется, а просроченный считается брошенным
    /// </summary>
    [Fact]
    public async Task A_run_left_over_from_before_a_restart_is_not_started_twice()
    {
        using var dir = new TempDir();
        var (runner, store) = Create(dir);
        var job = Samples.Job("slow", Samples.OnStart);
        var now = DateTimeOffset.Now;

        store.Write(Samples.Status("slow", ProcessStateEnum.Running, now.AddMinutes(-1)));
        Assert.Empty(new JobScheduler([job], store, runner, TimeZoneInfo.Local, TimeSpan.FromMinutes(10)).Tick(now));

        store.Write(Samples.Status("slow", ProcessStateEnum.Running, now.AddMinutes(-90)));
        var started = new JobScheduler([job], store, runner, TimeZoneInfo.Local, TimeSpan.FromMinutes(10)).Tick(now);
        await Task.WhenAll(started);
        Assert.True(started.Count == 1, "брошенный запуск старше таймаута должен быть выполнен заново");
    }

    /// <summary>
    /// Выключенная задача сама не запускается, но запускается вручную
    /// </summary>
    [Fact]
    public async Task A_disabled_job_never_starts_by_itself_but_can_be_run_by_hand()
    {
        using var dir = new TempDir();
        var (runner, store) = Create(dir);
        var job = Samples.Job("off", Samples.OnStart, Samples.Every(1)) with { Enabled = false };
        var scheduler = new JobScheduler([job], store, runner, TimeZoneInfo.Local, TimeSpan.FromMinutes(10));

        Assert.Empty(scheduler.Tick(DateTimeOffset.Now));
        Assert.Null(scheduler.NextRun(job, DateTimeOffset.Now));

        var manual = scheduler.RunNow("off");
        Assert.NotNull(manual);
        Assert.Equal("manual", (await manual).Reason);
        Assert.Null(scheduler.RunNow("missing"));
    }
}
