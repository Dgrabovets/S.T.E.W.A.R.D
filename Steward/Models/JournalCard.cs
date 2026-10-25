using System.Globalization;
using Steward.Monitoring.Models;

namespace Steward.Models;

/// <summary>
/// Карточка одной сверки для списка в окне
/// </summary>
public sealed class JournalCard
{
    /// <summary>
    /// Подпись дня. Пустая, если день уже показан карточкой выше
    /// </summary>
    public string Day { get; init; } = string.Empty;

    /// <summary>
    /// Над карточкой нужно нарисовать день
    /// </summary>
    public bool ShowDay { get; init; }

    /// <summary>
    /// Время прогона
    /// </summary>
    public string Time { get; init; } = string.Empty;

    /// <summary>
    /// Короткий итог
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Подробности в несколько строк
    /// </summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>
    /// Тон карточки
    /// </summary>
    public LogTone Tone { get; init; }

    #region Cards

    /// <summary>
    /// Собирает карточки из записей журнала
    /// </summary>
    /// <param name="entries">Записи от новых к старым</param>
    /// <returns>Карточки с подписью дня только у первой записи этого дня</returns>
    public static IReadOnlyList<JournalCard> From(IReadOnlyList<LogEntry> entries)
    {
        var cards = new List<JournalCard>();
        var previousDay = DateTime.MinValue;
        foreach (var entry in entries)
        {
            var day = entry.Time.Date;
            cards.Add(new JournalCard
            {
                ShowDay = day != previousDay,
                Day = DayLabel(entry.Time),
                Time = entry.Time.ToString("HH:mm", CultureInfo.InvariantCulture),
                Title = entry.Title,
                Detail = ShownDetail(entry),
                Tone = entry.Tone,
            });
            previousDay = day;
        }

        return cards;
    }

    #endregion Cards

    #region Wording

    /// <summary>
    /// Добавляет к тихому прогону число таких же сверок
    /// </summary>
    /// <param name="entry">Запись журнала</param>
    /// <returns>Текст подробностей</returns>
    private static string ShownDetail(LogEntry entry)
    {
        if (entry.Tone != LogTone.Quiet || entry.Repeat <= 1)
            return entry.Detail;

        var more = entry.Repeat - 1;
        return entry.Detail + "\n" + (more == 1 ? "И ещё одна такая сверка" : $"И ещё {Checks(more)}");
    }

    /// <summary>
    /// Склоняет слово «сверка»
    /// </summary>
    /// <param name="count">Число сверок</param>
    /// <returns>Число и слово в нужном падеже</returns>
    private static string Checks(int count)
    {
        var mod100 = count % 100;
        var mod10 = count % 10;
        var word = mod100 is >= 11 and <= 14
            ? "сверок"
            : mod10 == 1
                ? "сверка"
                : mod10 is >= 2 and <= 4
                    ? "сверки"
                    : "сверок";
        return $"{count} {word}";
    }

    /// <summary>
    /// Подпись дня
    /// </summary>
    /// <param name="time">Время записи</param>
    /// <returns>«Сегодня», «Вчера» или дата</returns>
    private static string DayLabel(DateTimeOffset time)
    {
        var day = time.Date;
        if (day == DateTime.Today)
            return "Сегодня";
        if (day == DateTime.Today.AddDays(-1))
            return "Вчера";

        return time.ToString("d MMMM", CultureInfo.GetCultureInfo("ru-RU"));
    }

    #endregion Wording
}
