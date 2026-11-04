namespace Steward.ViewModels;

/// <summary>
/// Столбец небольшого графика: подпись под столбцом, число над ним и высота
/// </summary>
/// <param name="Label">Подпись, например день недели</param>
/// <param name="Value">Число над столбцом</param>
/// <param name="Height">Высота столбца в пикселях</param>
public sealed record ChartBarViewModel(string Label, int Value, double Height);
