using Steward.Monitoring.Models;
using Steward.Status.Rows;

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
