namespace Steward.Models;

/// <summary>
/// Общий итог по всем задачам: цвет плашки и две фразы
/// </summary>
/// <param name="Tone">Цвет плашки</param>
/// <param name="Label">Короткий текст плашки</param>
/// <param name="Subtitle">Строка под заголовком</param>
public sealed record TaskSummary(PanelToneEnum Tone, string Label, string Subtitle)
{
    /// <summary>
    /// Считает итог. Выключенные задачи в нём не участвуют
    /// </summary>
    /// <param name="rows">Строки задач</param>
    /// <returns>Сбой важнее ожидания решения, ожидание важнее покоя</returns>
    public static TaskSummary Of(IReadOnlyList<TaskStatusRow> rows)
    {
        var active = rows.Where(row => !row.Muted).ToArray();
        var problems = active.Where(row => row.Tone == PanelToneEnum.Problem).Select(row => row.Name).ToArray();
        if (problems.Length > 0)
            return new TaskSummary(PanelToneEnum.Problem, "Сбой", "Требует внимания: " + string.Join(", ", problems));

        var waiting = active.Where(row => row.Tone == PanelToneEnum.Attention).Select(row => row.Name).ToArray();
        if (waiting.Length > 0)
            return new TaskSummary(PanelToneEnum.Attention, "Ждёт тебя", "Ждут решения: " + string.Join(", ", waiting));

        return active.All(row => row.Tone == PanelToneEnum.Running)
            ? new TaskSummary(PanelToneEnum.Running, "Работает", "Все задачи работают")
            : new TaskSummary(PanelToneEnum.Idle, "Не всё запущено", "Не все задачи запущены");
    }
}
