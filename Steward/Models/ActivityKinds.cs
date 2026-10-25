namespace Steward.Models;

/// <summary>
/// Подбирает вид движения индикатора по задаче
/// </summary>
public static class ActivityKinds
{
    /// <summary>
    /// Вид движения для задачи из реестра
    /// </summary>
    /// <param name="jobId">Идентификатор задачи из config/jobs.json</param>
    /// <returns>Связь для почты и бота, сборка для домашних заданий, остальным ровное вращение</returns>
    public static ActivityKindEnum For(string jobId) => jobId switch
    {
        "mail" or "reels" or "telegram" => ActivityKindEnum.Connecting,
        "homework" => ActivityKindEnum.Composing,
        _ => ActivityKindEnum.Working,
    };
}