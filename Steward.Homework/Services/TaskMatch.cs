namespace Steward.Homework.Services;

/// <summary>
/// Единое правило «это одно и то же задание», по которому сверяются список заданий, отрывки расшифровки и задачи Singularity
/// </summary>
public static class TaskMatch
{
    /// <summary>
    /// Сравнивает два задания: совпадает название или слова преподавателя, без учёта регистра и пробелов по краям. Пустое название или пустая цитата не совпадают ни с чем
    /// </summary>
    /// <param name="titleA">Название первого задания</param>
    /// <param name="quoteA">Слова преподавателя у первого задания</param>
    /// <param name="titleB">Название второго задания</param>
    /// <param name="quoteB">Слова преподавателя у второго задания</param>
    /// <returns>
    /// <see langword="true"/>, если это одно задание
    /// </returns>
    public static bool Same(string titleA, string quoteA, string titleB, string quoteB) =>
        (titleA.Trim().Length > 0 && string.Equals(titleA.Trim(), titleB.Trim(), StringComparison.OrdinalIgnoreCase))
        || (quoteA.Trim().Length > 0 && string.Equals(quoteA.Trim(), quoteB.Trim(), StringComparison.OrdinalIgnoreCase));
}
