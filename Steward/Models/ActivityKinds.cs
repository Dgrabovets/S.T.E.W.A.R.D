namespace Steward.Models;

/// <summary>
/// Подбирает вид сферы по задаче и её состоянию
/// </summary>
public static class ActivityKinds
{
    /// <summary>
    /// Вид движения для выполняющейся задачи из реестра
    /// </summary>
    /// <param name="jobId">Идентификатор задачи из config/jobs.json</param>
    /// <returns>Связь для почты и бота, остальным ровное вращение</returns>
    public static ActivityKindEnum For(string jobId) => jobId switch
    {
        "mail" or "reels" or "telegram" => ActivityKindEnum.Connecting,
        _ => ActivityKindEnum.Working,
    };

    /// <summary>
    /// Вид сферы для задачи: пока выполняется, по её делу, иначе по состоянию
    /// </summary>
    /// <param name="row">Строка задачи</param>
    /// <returns>Вид сферы</returns>
    public static ActivityKindEnum Of(TaskStatusRow row) => row switch
    {
        { Working: true } => For(row.Id),
        { Muted: true } => ActivityKindEnum.Dormant,
        { Tone: PanelToneEnum.Problem } => ActivityKindEnum.Failed,
        { Tone: PanelToneEnum.Attention } => ActivityKindEnum.Attention,
        { Tone: PanelToneEnum.Running } when row.Headline.StartsWith("Выполнено", StringComparison.Ordinal) => ActivityKindEnum.Waiting,
        { Tone: PanelToneEnum.Running } => ActivityKindEnum.Alive,
        _ => ActivityKindEnum.Dormant,
    };
}