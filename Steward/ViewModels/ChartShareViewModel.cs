using Steward.Models;

namespace Steward.ViewModels;

/// <summary>
/// Строка графика долей: название, число и доля от наибольшего значения
/// </summary>
/// <param name="Title">Название, например «Ответить»</param>
/// <param name="Value">Число</param>
/// <param name="Share">Доля от наибольшего значения, от 0 до 1</param>
/// <param name="Tone">Цвет полоски</param>
public sealed record ChartShareViewModel(string Title, int Value, double Share, PanelToneEnum Tone);
