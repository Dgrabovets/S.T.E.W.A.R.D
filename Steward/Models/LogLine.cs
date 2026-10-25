namespace Steward.Models;

/// <summary>
/// Одна запись из вывода программы для журнала: время, текст и цвет плашки
/// </summary>
/// <param name="Time">Время записи вида <c>25.10.2026 18:18:44</c> или пустая строка, если у записи его нет</param>
/// <param name="Text">Текст записи. Строки трассировки стека, которые идут следом, входят в тот же текст</param>
/// <param name="Tone">Цвет: ошибка красная, письма к действию жёлтые, остальное серое</param>
public sealed record LogLine(string Time, string Text, PanelToneEnum Tone);