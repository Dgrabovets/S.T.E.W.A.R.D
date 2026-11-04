using Steward.Status.Running;

namespace Steward.Status.Tests;

/// <summary>
/// Проверяет журнал запусков в файле
/// </summary>
public class RunHistoryTests
{
    /// <summary>
    /// Запись журнала для проверок
    /// </summary>
    private static readonly RunRecord Record = new("mail", "Почта", "manual", Samples.Winter(2026, 10, 25, 9), Samples.Winter(2026, 10, 25, 9, 1), 0, RunOutcomeEnum.Ok, null);

    /// <summary>
    /// После очистки журнал пуст, а новые записи снова пишутся
    /// </summary>
    [Fact]
    public void Clear_empties_the_journal_and_it_keeps_working()
    {
        using var dir = new TempDir();
        var history = new RunHistory(dir.Path);
        history.Append(Record);
        history.Append(Record);
        Assert.Equal(2, history.Load(10).Count);

        history.Clear();

        Assert.Empty(history.Load(10));
        history.Append(Record);
        Assert.Single(history.Load(10));
    }

    /// <summary>
    /// Удаление одной записи убирает только её: такую же по задаче, но с другим временем начала и записи других задач оставляет
    /// </summary>
    [Fact]
    public void Remove_deletes_only_the_matching_record_and_clear_by_job_keeps_the_others()
    {
        using var dir = new TempDir();
        var history = new RunHistory(dir.Path);
        var later = Record with { StartedAt = Record.StartedAt.AddHours(1), FinishedAt = Record.FinishedAt.AddHours(1) };
        var other = Record with { JobId = "reels", Title = "Рилсы" };
        history.Append(Record);
        history.Append(later);
        history.Append(other);

        history.Remove(Record);

        Assert.Equal([later.StartedAt, other.StartedAt], history.Load(10).Select(record => record.StartedAt).Reverse().ToArray());

        history.Clear("mail");

        Assert.Equal(["reels"], history.Load(10).Select(record => record.JobId).ToArray());
    }

    /// <summary>
    /// Очистка без файла журнала ничего не ломает
    /// </summary>
    [Fact]
    public void Clear_without_a_file_does_nothing()
    {
        using var dir = new TempDir();

        new RunHistory(dir.Path).Clear();

        Assert.Empty(new RunHistory(dir.Path).Load(10));
    }

    /// <summary>
    /// Записи со сводкой и выводом читаются обратно, а старая строка без этих полей тоже читается
    /// </summary>
    [Fact]
    public void Summary_and_output_round_trip_and_old_lines_still_load()
    {
        using var dir = new TempDir();
        var history = new RunHistory(dir.Path);
        history.Append(Record with { Summary = "Прочее: 1", Output = "строка 1\nстрока 2" });
        File.AppendAllText(Path.Combine(dir.Path, "runs.jsonl"), "{\"jobId\":\"old\",\"title\":\"Старая\",\"reason\":\"daily\",\"startedAt\":\"2026-10-24T09:00:00+01:00\",\"finishedAt\":\"2026-10-24T09:00:05+01:00\",\"exitCode\":0,\"outcome\":\"Ok\",\"detail\":null}\n");

        var loaded = history.Load(10);

        Assert.Equal(["old", "mail"], loaded.Select(record => record.JobId).ToArray());
        Assert.Null(loaded[0].Output);
        Assert.Equal("строка 1\nстрока 2", loaded[1].Output);
        Assert.Equal("Прочее: 1", loaded[1].Summary);
    }

    /// <summary>
    /// Когда файл журнала вырос больше предела, он сжимается до последних записей, а новые записи по-прежнему пишутся
    /// </summary>
    [Fact]
    public void A_journal_over_the_limit_is_trimmed_to_the_latest_records()
    {
        using var dir = new TempDir();
        var history = new RunHistory(dir.Path);
        var fat = Record with { Output = new string('x', 4000) };

        for (var index = 0; index < 400; index++)
            history.Append(fat with { StartedAt = fat.StartedAt.AddMinutes(index), FinishedAt = fat.FinishedAt.AddMinutes(index) });

        var lines = File.ReadLines(Path.Combine(dir.Path, "runs.jsonl")).Count();
        Assert.InRange(lines, 1, 300);
        Assert.Equal(fat.StartedAt.AddMinutes(399), history.Load(1).Single().StartedAt);
    }
}
