namespace Steward.Models;

/// <summary>
/// Образец цвета для страницы «Дизайн»: имя, кисть и шестнадцатеричная запись
/// </summary>
public sealed class ColorSwatch
{
    /// <summary>
    /// Берёт кисть и цвет из ресурсов по имени без суффиксов
    /// </summary>
    /// <param name="name">Имя цвета: <c>Surface</c> даёт <c>SurfaceBrush</c> и <c>SurfaceColor</c></param>
    public ColorSwatch(string name)
    {
        Name = name;
        Brush = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource(name + "Brush");
        Hex = ((System.Windows.Media.Color)System.Windows.Application.Current.FindResource(name + "Color")).ToString().Replace("#FF", "#", StringComparison.Ordinal);
    }

    /// <summary>
    /// Имя цвета
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Кисть цвета
    /// </summary>
    public System.Windows.Media.Brush Brush { get; }

    /// <summary>
    /// Значение вида <c>#1B2336</c>
    /// </summary>
    public string Hex { get; }
}