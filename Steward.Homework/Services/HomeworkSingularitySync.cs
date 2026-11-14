using Steward.Homework.Storage;
using Steward.Mail.Interfaces;
using Steward.Mail.Models;

namespace Steward.Homework.Services;

/// <summary>
/// Сверяет задачи по домашним заданиям, которые программа помнит у себя, с тем, что сейчас лежит в SingularityApp: удалённая там задача забывается, и кнопка «Добавить в Singularity» снова доступна
/// </summary>
public static class HomeworkSingularitySync
{
    /// <summary>
    /// Общее начало ключей задач ДЗ: <c>hw:</c> для заданий списка и <c>hwq:</c> для заданий из отрывков расшифровки
    /// </summary>
    private const string KEY_PREFIX = "hw";

    /// <summary>
    /// Читает из SingularityApp задачи ДЗ и забывает те, которых там больше нет или которые удалены. Выполненные и архивные задачи остаются: задание по ним уже заведено
    /// </summary>
    /// <param name="store">Папка домашних заданий</param>
    /// <param name="notes">Чтение задач SingularityApp</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Ключи, которые забыты. Пусто, если всё совпало или помнить было нечего</returns>
    /// <exception cref="HttpRequestException">SingularityApp не ответил: ничего не забывается</exception>
    public static async Task<IReadOnlyList<string>> ReconcileAsync(HomeworkStore store, ISingularityNotes notes, CancellationToken cancellationToken)
    {
        var dates = store.LoadLessons().ToDictionary(lesson => lesson.Id, lesson => lesson.Date, StringComparer.Ordinal);
        var remembered = store.LoadCreated().Where(key => dates.ContainsKey(LessonOf(key))).ToList();
        if (remembered.Count == 0)
            return [];

        // Задача начинается не раньше дня занятия, поэтому границей служит самое раннее из занятий с задачами; по занятиям вне окна ничего не решается
        var from = remembered.Min(key => dates[LessonOf(key)]).AddDays(-1);
        var tasks = await notes.ListTasksAsync(new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeZoneInfo.Local.GetUtcOffset(DateTime.Now)), KEY_PREFIX, cancellationToken);

        return Forget(store, remembered, tasks);
    }

    /// <summary>
    /// Забывает ключи, у которых нет живой задачи среди найденных
    /// </summary>
    /// <param name="store">Папка домашних заданий</param>
    /// <param name="remembered">Ключи, которые программа считает заведёнными</param>
    /// <param name="tasks">Задачи SingularityApp с ключом ДЗ, в том числе удалённые</param>
    /// <returns>Забытые ключи</returns>
    public static IReadOnlyList<string> Forget(HomeworkStore store, IReadOnlyList<string> remembered, IReadOnlyList<SingularityTaskInfo> tasks)
    {
        var alive = tasks.Where(task => !task.Removed).Select(task => task.ExternalId).ToHashSet(StringComparer.Ordinal);
        var gone = remembered.Where(key => !alive.Contains(key)).ToList();
        if (gone.Count > 0)
            store.UnmarkCreated(gone);

        return gone;
    }

    /// <summary>
    /// Ключи задач по заданиям, которые пользователь удалил в SingularityApp: автоматически их заново не заводят
    /// </summary>
    /// <param name="tasks">Задачи SingularityApp с ключом ДЗ, в том числе удалённые</param>
    /// <returns>Ключи удалённых задач, у которых нет живой копии</returns>
    public static IReadOnlySet<string> DeletedKeys(IEnumerable<SingularityTaskInfo> tasks)
    {
        var list = tasks.ToList();
        var alive = list.Where(task => !task.Removed).Select(task => task.ExternalId).ToHashSet(StringComparer.Ordinal);

        return list.Where(task => task.Removed && !alive.Contains(task.ExternalId)).Select(task => task.ExternalId).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Ключ занятия из ключа задачи вида <c>hw:&lt;занятие&gt;:&lt;номер&gt;</c> или <c>hwq:&lt;занятие&gt;:&lt;номер&gt;</c>
    /// </summary>
    /// <param name="key">Ключ задачи</param>
    /// <returns>Ключ занятия или пустая строка, если ключ не такого вида</returns>
    private static string LessonOf(string key)
    {
        var parts = key.Split(':');

        return parts.Length >= 3 && parts[0] is "hw" or "hwq" ? string.Join(':', parts[1..^1]) : string.Empty;
    }
}
