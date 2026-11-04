using Steward.Schedule.Scheduling.Models;

namespace Steward.Schedule.Scheduling.Implementations;

/// <summary>
/// Выбирает вкладки месяцев, которые нужно читать сегодня: прошлый месяц, текущий и следующий, если такие вкладки есть в таблице
/// </summary>
public static class ScheduleTabs
{
    /// <summary>
    /// Корни русских названий месяцев по порядку от января. Вкладка может называться «Ноябрь», «НОЯБРЬ 2026» или «ноябрь (новое)»: ищется корень, а не точное слово
    /// </summary>
    private static readonly string[] MonthRoots =
    [
        "январ", "феврал", "март", "апрел", "ма", "июн", "июл", "август", "сентябр", "октябр", "ноябр", "декабр",
    ];

    /// <summary>
    /// Определяет месяц по названию вкладки
    /// </summary>
    /// <param name="name">Название вкладки</param>
    /// <returns>Номер месяца от 1 до 12 или <see langword="null"/>, если название не похоже на месяц</returns>
    public static int? MonthOf(string name)
    {
        // Первое слово названия: так «май» не находится внутри другого слова, а год и пометки после названия не мешают
        var word = new string([.. name.Trim().ToLowerInvariant().SkipWhile(symbol => !char.IsLetter(symbol)).TakeWhile(char.IsLetter)]);
        for (var index = 0; index < MonthRoots.Length; index++)
        {
            var root = MonthRoots[index];
            var exact = root == "ма" ? word is "май" or "мая" : word.StartsWith(root, StringComparison.Ordinal);
            if (exact)
                return index + 1;
        }

        return null;
    }

    /// <summary>
    /// Оставляет вкладки прошлого, текущего и следующего месяцев по порядку от старой к новой. Прошлый месяц нужен для недели, которая начинается в нём, следующий читается, как только его вкладка появилась в таблице
    /// </summary>
    /// <param name="sheets">Все листы книги: название вкладки и CSV</param>
    /// <param name="today">Сегодняшняя дата в часовом поясе таблицы</param>
    /// <returns>Нужные вкладки. Пусто, если в таблице нет ни одной вкладки с названием месяца из этого окна</returns>
    public static IReadOnlyList<ScheduleSheet> Choose(IReadOnlyList<(string Name, string Csv)> sheets, DateOnly today)
    {
        var previous = today.AddMonths(-1).Month;
        var current = today.Month;
        var next = today.AddMonths(1).Month;
        var order = new[] { previous, current, next };

        return
        [
            .. sheets
                .Select(sheet => new ScheduleSheet(sheet.Name, MonthOf(sheet.Name), sheet.Csv))
                .Where(sheet => sheet.Month is { } month && order.Contains(month))
                .OrderBy(sheet => Array.IndexOf(order, sheet.Month!.Value)),
        ];
    }
}
