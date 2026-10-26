namespace Steward.Models;

/// <summary>
/// Круглая кнопка меню из капель
/// </summary>
/// <param name="Glyph">Символ значка из шрифта Segoe Fluent Icons</param>
/// <param name="Label">Подпись, которая появляется над кнопкой при наведении</param>
/// <param name="Activate">Действие кнопки</param>
/// <param name="DividerBefore">Перед кнопкой рисуется вертикальная черта и добавляется промежуток: так кнопки делятся на группы</param>
public sealed record GooeyItem(string Glyph, string Label, Action Activate, bool DividerBefore = false);