using Steward.Models;
using Steward.Monitoring.Implementations;
using Steward.Monitoring.Models;
using Steward.Schedule.Common.Constants;
using Steward.Status.Registry;
using Steward.Status.Running;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Проверяет, что журнал показывает, что именно поменялось в расписании, копит историю сверок и раскладывает записи по задачам
/// </summary>
public class ScheduleJournalTests
{
    /// <summary>
    /// Момент для записей: час передаётся числом, чтобы порядок записей читался в тестах
    /// </summary>
    private static DateTimeOffset At(int hour) => new(2026, 10, 26, hour, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Запись сверки с нужным тоном
    /// </summary>
    private static LogEntry Entry(int hour, LogToneEnum tone = LogToneEnum.Update, string detail = "x", int repeat = 1) => new(At(hour), tone, "т", detail, repeat);

    /// <summary>
    /// Перечень отличий, который пишет сервис перед обновлением, превращается в строки: что было и что стало
    /// </summary>
    [Fact]
    public void An_update_lists_what_exactly_changed()
    {
        const string stamp = "2026-10-26T10:00:00.000000000Z";
        var logs = string.Join('\n',
            $"schedule-1  | {stamp}       {SyncLogMessages.CHANGES}up:game:2026-10-29:2010: start=2026-10-29T17:10:00Z>2026-10-29T21:00:00+03:00 | title=Старое>Новое | length=90>60 | note",
            $"schedule-1  | {stamp}       {SyncLogMessages.UPDATED}up:game:2026-10-29:2010 Новое",
            $"schedule-1  | {stamp}       {SyncLogMessages.FINISHED}created=0 updated=1 unarchived=0 completed=0 skipped=0");

        var entry = LogJournal.Read(logs).Single();

        Assert.Equal(
            "Обновлено «Новое»\n• время: 29.10 в 20:10 → 29.10 в 21:00\n• название: «Старое» → «Новое»\n• длительность: 90 мин → 60 мин\n• заметка к занятию",
            entry.Detail);
    }

    /// <summary>
    /// Обновление без перечня отличий остаётся одной строкой
    /// </summary>
    [Fact]
    public void An_update_without_a_list_stays_one_line()
    {
        const string stamp = "2026-10-26T10:00:00.000000000Z";
        var logs = string.Join('\n',
            $"schedule-1  | {stamp}       {SyncLogMessages.UPDATED}up:game:2026-10-29:2010 Новое",
            $"schedule-1  | {stamp}       {SyncLogMessages.FINISHED}created=0 updated=1 unarchived=0 completed=0 skipped=0");

        Assert.Equal("Обновлено «Новое»", LogJournal.Read(logs).Single().Detail);
    }

    /// <summary>
    /// Свежие записи ложатся поверх сохранённых, граничная запись не двоится, а старые не теряются
    /// </summary>
    [Fact]
    public void History_keeps_older_entries_and_does_not_double_the_boundary()
    {
        var stored = new[] { Entry(3), Entry(2), Entry(1) };
        var fresh = new[] { Entry(4), Entry(3) };

        var merged = ScheduleHistory.Merge(stored, fresh);

        Assert.Equal([At(4), At(3), At(2), At(1)], merged.Select(entry => entry.Time).ToArray());
    }

    /// <summary>
    /// Если журнал контейнера ушёл вперёд и стыка нет, все свежие записи добавляются сверху
    /// </summary>
    [Fact]
    public void History_takes_all_fresh_entries_when_the_tails_do_not_meet()
    {
        var merged = ScheduleHistory.Merge([Entry(1)], [Entry(3), Entry(2)]);

        Assert.Equal([At(3), At(2), At(1)], merged.Select(entry => entry.Time).ToArray());
    }

    /// <summary>
    /// Пустой хвост журнала (Docker выключен) оставляет историю как есть, а длина ограничена
    /// </summary>
    [Fact]
    public void History_survives_an_empty_tail_and_is_capped()
    {
        var stored = new[] { Entry(2), Entry(1) };

        Assert.Equal(stored, ScheduleHistory.Merge(stored, []));

        var many = Enumerable.Range(0, ScheduleHistory.LIMIT + 20).Select(index => new LogEntry(At(0).AddMinutes(-index), LogToneEnum.Update, "т", $"{index}", 1)).ToArray();
        Assert.Equal(ScheduleHistory.LIMIT, ScheduleHistory.Merge(many, [many[0]]).Count);
    }

    /// <summary>
    /// Журнал делится на разделы по задачам, они стоят по идентификатору, а не по свежести, и раздел с неудачей раскрыт
    /// </summary>
    [Fact]
    public void The_journal_groups_runs_by_job_in_a_fixed_order_and_opens_a_failure()
    {
        var now = DateTimeOffset.Now;
        var model = new MainViewModel();
        var snapshot = new SyncSnapshot { DockerAvailable = true, ContainerRunning = true, Entries = [new LogEntry(now.AddHours(-3), LogToneEnum.Quiet, "Всё совпало", "Новых правок в таблице нет", 1)] };
        var records = new[]
        {
            new RunRecord("mail", "Почта", "manual", now.AddMinutes(-10), now.AddMinutes(-9), 0, RunOutcomeEnum.Ok, null),
            new RunRecord("mts", "MTS Link", JobTrigger.DAILY, now.AddHours(-1), now.AddHours(-1).AddSeconds(5), 1, RunOutcomeEnum.Failed, "нет доступа"),
            new RunRecord("mail", "Почта", JobTrigger.DAILY, now.AddDays(-1), now.AddDays(-1).AddSeconds(5), 0, RunOutcomeEnum.Ok, null),
        };

        model.Apply(snapshot, [], records, [], []);

        Assert.Equal(["mail", "mts", "schedule"], model.Groups.Select(group => group.Id).ToArray());
        Assert.Single(model.Groups[0].Runs);
        Assert.True(model.Groups[0].Runs[0].HasRepeats);
        Assert.StartsWith("Таких запусков подряд: 2", model.Groups[0].Runs[0].Repeats, StringComparison.Ordinal);
        Assert.False(model.Groups[0].IsOpen);
        Assert.True(model.Groups[1].IsOpen);
        Assert.Single(model.Groups[2].Checks);
        Assert.True(model.Groups[0].Runs[0].IsManual);
    }

    /// <summary>
    /// Обновление данных не пересоздаёт разделы и не сбрасывает то, что человек раскрыл
    /// </summary>
    [Fact]
    public void Refreshing_keeps_the_group_objects_and_the_choice_of_the_user()
    {
        var now = DateTimeOffset.Now;
        var model = new MainViewModel();
        var records = new[] { new RunRecord("mail", "Почта", "manual", now.AddMinutes(-10), now.AddMinutes(-9), 0, RunOutcomeEnum.Ok, null) };
        model.Apply(new SyncSnapshot(), [], records, [], []);
        var first = model.Groups.Single();
        first.IsOpen = true;

        model.Apply(new SyncSnapshot(), [], records, [], []);

        Assert.Same(first, model.Groups.Single());
        Assert.True(model.Groups.Single().IsOpen);
    }

    /// <summary>
    /// Записи листаются страницами: размер выбирается, стрелки не выходят за края, а при смене размера видимая запись остаётся на экране
    /// </summary>
    [Fact]
    public void Entries_are_paged_with_a_chosen_step_and_the_arrows_stay_inside()
    {
        var start = DateTimeOffset.Now;
        var rows = Enumerable.Range(0, 23).Select(index => new RunRowViewModel(new RunRecord("mail", "Почта", "manual", start.AddMinutes(-index), start.AddMinutes(-index).AddSeconds(2), 0, RunOutcomeEnum.Ok, null), _ => { })).ToArray();
        var group = new JournalGroupViewModel("mail", "Почта", true, (_, _) => { }, _ => { });

        group.Update("v1", "23 записи", PanelToneEnum.Running, ActivityKindEnum.Sealed, rows, []);

        Assert.Equal((5, 5, "1 из 5", true), (group.Runs.Count, group.PageCount, group.PageLabel, group.HasPaging));
        Assert.False(group.PreviousCommand.CanExecute(null));

        for (var step = 0; step < 4; step++)
            group.NextCommand.Execute(null);

        Assert.Equal((3, "5 из 5"), (group.Runs.Count, group.PageLabel));
        Assert.False(group.NextCommand.CanExecute(null));
        group.NextCommand.Execute(null);
        Assert.Equal("5 из 5", group.PageLabel);

        group.PageSize = 10;
        Assert.Equal((3, 3, "3 из 3"), (group.Runs.Count, group.PageCount, group.PageLabel));
        Assert.Same(rows[20], group.Runs[0]);

        group.Update("v2", "5 записей", PanelToneEnum.Running, ActivityKindEnum.Sealed, rows[..5], []);
        Assert.Equal(("1 из 1", false), (group.PageLabel, group.HasPaging));
    }

    /// <summary>
    /// Строка сверки с правками показывается выделенным блоком, а тихая обходится без него
    /// </summary>
    [Fact]
    public void A_check_with_changes_gets_a_block_and_a_quiet_one_does_not()
    {
        var changed = new ScheduleCheckViewModel(Entry(5, LogToneEnum.Update, "Обновлено «А»\n• теги"));
        var quiet = new ScheduleCheckViewModel(Entry(5, LogToneEnum.Quiet, "Новых правок в таблице нет", 3));

        Assert.True(changed.HasBlock);
        Assert.False(quiet.HasBlock);
        Assert.True(quiet.HasQuietDetail);
        Assert.Equal("Таких сверок подряд: 3", quiet.Repeats);
    }
}
