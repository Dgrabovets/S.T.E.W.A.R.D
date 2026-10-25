namespace Steward.Ideas.Models;

/// <summary>
/// Что решено с идеей
/// </summary>
public enum IdeaStateEnum
{
    /// <summary>
    /// Идея пришла и ждёт решения: «Делаем» или «Отклонить»
    /// </summary>
    New,

    /// <summary>
    /// Решено делать: идея передана Claude
    /// </summary>
    Approved,

    /// <summary>
    /// Решено не делать
    /// </summary>
    Rejected,
}