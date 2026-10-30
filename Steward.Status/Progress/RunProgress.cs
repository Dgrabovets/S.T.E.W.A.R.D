namespace Steward.Status.Progress;

/// <summary>
/// Что сейчас делает процесс: шаги запуска по порядку, текущий шаг, счётчик внутри шага и пояснение
/// </summary>
/// <param name="Stages">Названия шагов запуска по порядку</param>
/// <param name="Current">Номер текущего шага, с нуля</param>
/// <param name="Done">Сколько единиц работы внутри шага уже сделано</param>
/// <param name="Total">Сколько всего единиц работы в шаге. Ноль: счётчика нет, показывается только название</param>
/// <param name="Detail">Что именно обрабатывается сейчас, например «Гейм-дизайн, 25.10.2026»</param>
/// <param name="Started">Когда начался запуск</param>
/// <param name="Updated">Когда процесс последний раз обновил этот файл</param>
public sealed record RunProgress(IReadOnlyList<string> Stages, int Current, int Done, int Total, string Detail, DateTimeOffset Started, DateTimeOffset Updated)
{
    /// <summary>
    /// Доля готовности текущего шага от нуля до единицы. Без счётчика ноль
    /// </summary>
    public double StageShare => Total > 0 ? Math.Clamp((double)Done / Total, 0, 1) : 0;

    /// <summary>
    /// Название текущего шага или пустая строка, если номер вышел за список
    /// </summary>
    public string CurrentTitle => Current >= 0 && Current < Stages.Count ? Stages[Current] : string.Empty;
}
