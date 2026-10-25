using Steward.Monitoring.Models;

namespace Steward.Monitoring.Implementations;

/// <summary>
/// Выбирает из журнала то, о чём стоит сказать всплывающим сообщением: отмены, переносы и правки занятий
/// </summary>
public static class ChangeNotice
{
    /// <summary>
    /// Начала строк журнала, которые говорят об отмене, переносе или правке. Добавление новых занятий сюда не входит
    /// </summary>
    private static readonly string[] Prefixes = ["Убрано из таблицы", "Удал", "Обновлено"];

    /// <summary>
    /// Собирает текст уведомления из записей, появившихся после <paramref name="after"/>
    /// </summary>
    /// <param name="entries">Записи журнала от новых к старым</param>
    /// <param name="after">Момент самой свежей записи, о которой уже сказано. Более ранние и такие же записи пропускаются</param>
    /// <param name="newest">
    /// Момент самой свежей учтённой записи. Равен <paramref name="after"/>, если новых правок нет
    /// </param>
    /// <returns>
    /// Строки через точку с запятой от старых к новым или <see langword="null"/>, если сказать не о чем
    /// </returns>
    public static string? Build(IReadOnlyList<LogEntry> entries, DateTimeOffset after, out DateTimeOffset newest)
    {
        newest = after;
        var lines = new List<string>();

        foreach (var entry in entries.Where(entry => entry.Tone == LogTone.Update && entry.Time > after).Reverse())
        {
            if (entry.Time > newest)
                newest = entry.Time;

            lines.AddRange(entry.Detail
                .Split('\n')
                .Where(line => Prefixes.Any(prefix => line.StartsWith(prefix, StringComparison.Ordinal))));
        }

        return lines.Count == 0 ? null : string.Join("; ", lines.Distinct(StringComparer.Ordinal));
    }
}
