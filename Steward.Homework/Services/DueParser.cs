using System.Globalization;

namespace Steward.Homework.Services;

/// <summary>
/// Разбирает срок, который агент записал в разборе занятия словами или датой
/// </summary>
public static class DueParser
{
    /// <summary>
    /// Разбирает срок. Год берётся из даты занятия, а срок раньше занятия переносится на следующий год
    /// </summary>
    /// <param name="text">Срок как записан в разборе: «02.11», «02.11.2026», «2026-11-02» или слова</param>
    /// <param name="lessonDate">Дата занятия</param>
    /// <returns>Дата или <see langword="null"/>, если срок не назван или не похож на дату</returns>
    public static DateOnly? Parse(string text, DateOnly lessonDate)
    {
        var value = text.Trim().TrimEnd('.');
        if (DateOnly.TryParseExact(value, ["yyyy-MM-dd", "dd.MM.yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var full))
            return full;

        if (!DateOnly.TryParseExact($"{value}.{lessonDate.Year}", "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var shortDate))
            return null;

        return shortDate < lessonDate ? shortDate.AddYears(1) : shortDate;
    }
}
