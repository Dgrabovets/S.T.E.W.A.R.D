namespace Steward.Models;

/// <summary>
/// Образец состояния для страницы «Дизайн»: цвет плашки и её подпись
/// </summary>
/// <param name="Tone">Цвет плашки</param>
/// <param name="Label">Подпись</param>
public sealed record ToneSample(PanelToneEnum Tone, string Label);