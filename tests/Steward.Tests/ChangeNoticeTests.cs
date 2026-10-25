using Steward.Monitoring.Implementations;
using Steward.Schedule.Common.Constants;

namespace Steward.Tests;

/// <summary>
/// Проверяет, что уведомление об отмене или переносе появляется один раз и не повторяется на каждом опросе
/// </summary>
public class ChangeNoticeTests
{
    #region Facts

    /// <summary>
    /// Убранное из таблицы занятие даёт текст, а следующий опрос, который читает тот же журнал заново, молчит
    /// </summary>
    [Fact]
    public void Build_reports_a_removed_lesson_once()
    {
        var logs = Run(SyncLogMessages.COMPLETED_REMOVED + "up:rki-2-26:2026-10-30:2010", 0);

        var text = ChangeNotice.Build(LogJournal.Read(logs), DateTimeOffset.MinValue, out var newest);
        var again = ChangeNotice.Build(LogJournal.Read(logs), newest, out var newestAgain);

        Assert.Equal("Убрано из таблицы 30.10 в 20:10", text);
        Assert.Null(again);
        Assert.Equal(newest, newestAgain);
    }

    /// <summary>
    /// Время записи берётся из метки Docker с девятью знаками после секунды, а не из часов: иначе уведомление повторялось бы на каждом опросе
    /// </summary>
    [Fact]
    public void Journal_takes_entry_time_from_the_docker_timestamp()
    {
        var entry = LogJournal.Read(Run(SyncLogMessages.COMPLETED_REMOVED + "up:rki-2-26:2026-10-30:2010", 0)).Single();

        Assert.Equal(new DateTime(2026, 10, 25, 9, 0, 0, DateTimeKind.Utc), entry.Time.UtcDateTime);
    }

    /// <summary>
    /// Добавление новых занятий не повод для уведомления, но момент всё равно сдвигается
    /// </summary>
    [Fact]
    public void Build_stays_silent_about_new_lessons()
    {
        var entries = LogJournal.Read(Run(SyncLogMessages.CREATED + "up:rki-2-26:2026-10-30:2010 Предмет", 0));

        var text = ChangeNotice.Build(entries, DateTimeOffset.MinValue, out var newest);

        Assert.Null(text);
        Assert.True(newest > DateTimeOffset.MinValue);
    }

    /// <summary>
    /// Правка и отмена из разных прогонов собираются в одну строку от старых к новым
    /// </summary>
    [Fact]
    public void Build_joins_changes_from_several_runs_oldest_first()
    {
        var logs = Run(SyncLogMessages.UPDATED + "up:rki-2-26:2026-10-30:2010 Предмет", 0) + "\n" + Run(SyncLogMessages.COMPLETED_REMOVED + "up:rki-2-26:2026-10-29:1720", 5);
        var entries = LogJournal.Read(logs);

        var text = ChangeNotice.Build(entries, DateTimeOffset.MinValue, out _);

        Assert.Equal("Обновлено «Предмет»; Убрано из таблицы 29.10 в 17:20", text);
    }

    /// <summary>
    /// Тихие прогоны без правок ничего не сообщают
    /// </summary>
    [Fact]
    public void Build_stays_silent_for_quiet_runs()
    {
        var entries = LogJournal.Read(Run(null, 0));

        var text = ChangeNotice.Build(entries, DateTimeOffset.MinValue, out var newest);

        Assert.Null(text);
        Assert.Equal(DateTimeOffset.MinValue, newest);
    }

    /// <summary>
    /// Итоговая строка из журнала, записанного до удаления счётчика архивных задач, читается так же
    /// </summary>
    [Fact]
    public void Journal_reads_the_old_finish_line_with_the_archived_counter()
    {
        var logs = $"schedule-1  | 2026-10-25T09:00:00.000000000Z       {SyncLogMessages.CREATED}up:rki-2-26:2026-10-30:2010 Предмет\n"
            + $"schedule-1  | 2026-10-25T09:00:00.000000000Z       {SyncLogMessages.FINISHED}created=1 updated=0 archived=0 unarchived=0 completed=0 skipped=2 deleted=0";

        var entry = LogJournal.Read(logs).Single();

        Assert.Equal(new DateTime(2026, 10, 25, 9, 0, 0, DateTimeKind.Utc), entry.Time.UtcDateTime);
    }

    #endregion Facts

    #region Helpers

    /// <summary>
    /// Собирает кусок журнала Docker с одним прогоном: необязательной правкой и итоговой строкой
    /// </summary>
    /// <param name="change">
    /// Строка действия синхронизации или <see langword="null"/>, если прогон тихий
    /// </param>
    /// <param name="minute">Минута прогона в часе 09:00 UTC</param>
    /// <returns>Строки с префиксом контейнера и меткой времени</returns>
    private static string Run(string? change, int minute)
    {
        var stamp = $"2026-10-25T09:{minute:00}:00.000000000Z";
        var lines = new List<string>();
        if (change is not null)
            lines.Add($"schedule-1  | {stamp}       {change}");

        lines.Add($"schedule-1  | {stamp}       {SyncLogMessages.FINISHED}created=0 updated=0 unarchived=0 completed=0 skipped=0 deleted=0");
        return string.Join('\n', lines);
    }

    #endregion Helpers
}
