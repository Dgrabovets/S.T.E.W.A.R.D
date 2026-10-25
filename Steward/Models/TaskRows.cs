using Steward.Status;
using Steward.Monitoring.Models;

namespace Steward.Models;

/// <summary>
/// Собирает общий список задач: расписание из Docker и задачи из реестра
/// </summary>
public static class TaskRows
{
    /// <summary>
    /// Идентификатор строки расписания. Она не запускается кнопкой: контейнером управляет Docker
    /// </summary>
    public const string SCHEDULE_ID = "schedule";

    /// <summary>
    /// Сводит снимок контейнера и строки задач в один список для панели
    /// </summary>
    /// <param name="snapshot">Состояние контейнера синхронизации</param>
    /// <param name="jobs">Строки задач из реестра</param>
    /// <returns>Сначала расписание, затем задачи по порядку реестра</returns>
    public static IReadOnlyList<TaskStatusRow> Build(SyncSnapshot snapshot, IReadOnlyList<JobRow> jobs)
    {
        var panel = PanelStatus.From(snapshot);
        var rows = new List<TaskStatusRow>
        {
            new(SCHEDULE_ID, "Расписание", panel.Label, $"{panel.Subtitle}. {panel.Summary}", panel.Tone),
        };

        rows.AddRange(jobs.Select(job => new TaskStatusRow(job.Id, job.Title, job.Headline, job.Detail, ToneOf(job.Tone), Muted: !job.Enabled, CanRun: job.Tone != RowToneEnum.Working)));
        return rows;
    }

    /// <summary>
    /// Переводит цвет строки задачи в цвет панели: выполняющаяся задача тоже зелёная
    /// </summary>
    /// <param name="tone">Цвет из библиотеки статусов</param>
    /// <returns>Цвет панели</returns>
    private static PanelToneEnum ToneOf(RowToneEnum tone) => tone switch
    {
        RowToneEnum.Ok or RowToneEnum.Working => PanelToneEnum.Running,
        RowToneEnum.Attention => PanelToneEnum.Attention,
        RowToneEnum.Problem => PanelToneEnum.Problem,
        _ => PanelToneEnum.Idle,
    };
}

/// <summary>
/// Общий итог по всем задачам: цвет плашки и две фразы
/// </summary>
/// <param name="Tone">Цвет плашки</param>
/// <param name="Label">Короткий текст плашки</param>
/// <param name="Subtitle">Строка под заголовком</param>
public sealed record TaskSummary(PanelToneEnum Tone, string Label, string Subtitle)
{
    /// <summary>
    /// Считает итог. Выключенные задачи в нём не участвуют
    /// </summary>
    /// <param name="rows">Строки задач</param>
    /// <returns>Сбой важнее ожидания решения, ожидание важнее покоя</returns>
    public static TaskSummary Of(IReadOnlyList<TaskStatusRow> rows)
    {
        var active = rows.Where(row => !row.Muted).ToArray();
        var problems = active.Where(row => row.Tone == PanelToneEnum.Problem).Select(row => row.Name).ToArray();
        if (problems.Length > 0)
            return new TaskSummary(PanelToneEnum.Problem, "Сбой", "Требует внимания: " + string.Join(", ", problems));

        var waiting = active.Where(row => row.Tone == PanelToneEnum.Attention).Select(row => row.Name).ToArray();
        if (waiting.Length > 0)
            return new TaskSummary(PanelToneEnum.Attention, "Ждёт тебя", "Ждут решения: " + string.Join(", ", waiting));

        return active.All(row => row.Tone == PanelToneEnum.Running)
            ? new TaskSummary(PanelToneEnum.Running, "Работает", "Все задачи работают")
            : new TaskSummary(PanelToneEnum.Idle, "Не всё запущено", "Не все задачи запущены");
    }
}
