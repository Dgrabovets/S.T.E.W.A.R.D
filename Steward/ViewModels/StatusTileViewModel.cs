using Steward.Models;

namespace Steward.ViewModels;

/// <summary>
/// Плитка состояния на странице «Статус»: подпись, значение и цвет, например «Docker» — «запущен»
/// </summary>
/// <param name="Label">Подпись. Пусто, если значение понятно из заголовка группы</param>
/// <param name="Value">Значение</param>
/// <param name="Tone">Цвет плитки</param>
public sealed record StatusTileViewModel(string Label, string Value, PanelToneEnum Tone)
{
    /// <summary>
    /// У плитки есть подпись
    /// </summary>
    public bool HasLabel => Label.Length > 0;
}
