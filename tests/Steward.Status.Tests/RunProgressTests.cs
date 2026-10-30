using Steward.Status.Progress;
using Steward.Status.State;

namespace Steward.Status.Tests;

/// <summary>
/// Проверяет файл прогресса запуска: запись по шагам, чтение, уборку и то, что он не мешается среди статусов процессов
/// </summary>
public class RunProgressTests
{
    /// <summary>
    /// Шаги запуска
    /// </summary>
    private static readonly string[] Stages = ["Занятия", "Расшифровки", "Разбор"];

    /// <summary>
    /// Отчёт сразу записывает первый шаг, переход к шагу и счётчик обновляют файл, а доля считается от общего числа
    /// </summary>
    [Fact]
    public void The_reporter_writes_the_stage_the_counter_and_the_detail()
    {
        using var dir = new TempDir();
        var store = new RunProgressStore(dir.StatusPath);
        var now = Samples.Winter(2026, 10, 25, 10);
        var reporter = new RunProgressReporter(store, "lms", Stages, () => now);

        Assert.Equal("Занятия", store.Read("lms")!.CurrentTitle);

        reporter.Enter(2, 4, "Гейм-дизайн, 25.10.2026");
        reporter.Step(1, "Базы данных, 18.10.2026");
        var progress = store.Read("lms")!;

        Assert.Equal(2, progress.Current);
        Assert.Equal("Разбор", progress.CurrentTitle);
        Assert.Equal(1, progress.Done);
        Assert.Equal(4, progress.Total);
        Assert.Equal("Базы данных, 18.10.2026", progress.Detail);
        Assert.Equal(0.25, progress.StageShare);
        Assert.Equal(now, progress.Started);
    }

    /// <summary>
    /// После окончания запуска файл убирается, а чтение отсутствующего, битого и пустого файла даёт «нет прогресса»
    /// </summary>
    [Fact]
    public void A_finished_missing_or_broken_file_means_no_progress()
    {
        using var dir = new TempDir();
        var store = new RunProgressStore(dir.StatusPath);
        var reporter = new RunProgressReporter(store, "lms", Stages);
        Assert.NotNull(store.Read("lms"));

        reporter.Finish();
        Assert.Null(store.Read("lms"));
        Assert.Null(store.Read("mail"));

        File.WriteAllText(Path.Combine(dir.StatusPath, "progress", "lms.json"), "{ не json");
        Assert.Null(store.Read("lms"));

        File.WriteAllText(Path.Combine(dir.StatusPath, "progress", "lms.json"), """{"stages":[],"current":0}""");
        Assert.Null(store.Read("lms"));
    }

    /// <summary>
    /// Файл прогресса лежит в подпапке, поэтому список статусов процессов его не видит, а плохой идентификатор не пускает запись за пределы папки
    /// </summary>
    [Fact]
    public void The_file_does_not_show_up_among_process_statuses_and_a_bad_id_is_refused()
    {
        using var dir = new TempDir();
        var store = new RunProgressStore(dir.StatusPath);
        _ = new RunProgressReporter(store, "lms", Stages);

        Assert.Empty(new StatusStore(dir.StatusPath).ReadAll());
        Assert.Throws<ArgumentException>(() => store.Write("../evil", new RunProgress(Stages, 0, 0, 0, string.Empty, DateTimeOffset.Now, DateTimeOffset.Now)));
        Assert.Null(store.Read("../evil"));
    }

    /// <summary>
    /// Сбой записи не ломает процесс: отчёт молча продолжает работать
    /// </summary>
    [Fact]
    public void A_write_failure_never_breaks_the_process()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "status"), "файл вместо папки");
        var reporter = new RunProgressReporter(new RunProgressStore(dir.StatusPath), "lms", Stages);

        reporter.Enter(1, 3, "что-то");
        reporter.Step(1);
        reporter.Finish();
    }
}
