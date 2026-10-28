using Steward.Models;

namespace Steward.ViewModels;

/// <summary>
/// Показатель настроек процесса, например «Токен бота» — «задан»: зелёный, когда всё в порядке, и жёлтый, когда нужно внимание
/// </summary>
/// <param name="Label">Что проверяется</param>
/// <param name="Value">Результат проверки</param>
/// <param name="Tone">Цвет значения</param>
public sealed record StatusChipViewModel(string Label, string Value, PanelToneEnum Tone);
