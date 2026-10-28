using Steward.Models;

namespace Steward.ViewModels;

/// <summary>
/// Короткая плашка состояния на странице процесса, например «Токен бота задан»: зелёная, когда всё в порядке, и жёлтая, когда нужно внимание
/// </summary>
/// <param name="Text">Текст плашки</param>
/// <param name="Tone">Цвет плашки</param>
public sealed record StatusChipViewModel(string Text, PanelToneEnum Tone);
