using Steward.Models;

namespace Steward.ViewModels;

/// <summary>
/// Плитка состояния на странице «Статус»: подпись, значение и цвет, например «Docker» — «запущен»
/// </summary>
/// <param name="Label">Подпись. Пусто, если значение понятно из заголовка группы</param>
/// <param name="Value">Значение</param>
/// <param name="Tone">Цвет плитки</param>
/// <param name="Note">Мелкая строка под значением, например «21 дн. назад». Пусто: строки нет</param>
public sealed record StatusTileViewModel(string Label, string Value, PanelToneEnum Tone, string Note = "")
{
    /// <summary>
    /// Под значением есть мелкая строка
    /// </summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>
    /// У плитки есть подпись
    /// </summary>
    public bool HasLabel => Label.Length > 0;
}
