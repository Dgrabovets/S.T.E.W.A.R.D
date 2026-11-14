namespace Steward.Mail.Models;

/// <summary>
/// Задача SingularityApp, найденная по внешнему ключу
/// </summary>
/// <param name="Id">Номер задачи в SingularityApp</param>
/// <param name="ExternalId">Внешний ключ задачи</param>
/// <param name="Checked">Отметка выполнения: 0 не выполнена, 1 выполнена, 2 отменена</param>
/// <param name="Removed">Задача удалена</param>
/// <param name="Archived">Задача в архиве</param>
/// <param name="Title">Название задачи. Пусто, если SingularityApp его не вернул</param>
/// <param name="Start">Начало задачи. <see langword="null"/>, если не задано</param>
public sealed record SingularityTaskInfo(string Id, string ExternalId, int Checked, bool Removed = false, bool Archived = false, string Title = "", DateTimeOffset? Start = null)
{
    /// <summary>
    /// Задача выполнена, отменена, удалена или в архиве: закрывать её не нужно, а заводить такую же заново нельзя
    /// </summary>
    public bool IsClosed => Checked != 0 || Removed || Archived;
}
