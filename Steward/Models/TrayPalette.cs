namespace Steward.Models;

/// <summary>
/// Цвета меню и значка в трее. Меню рисует WinForms, поэтому цвета берутся из тех же ресурсов, что и у окон: в коде они не повторяются
/// </summary>
public static class TrayPalette
{
    #region Colors

    /// <summary>
    /// Фон меню
    /// </summary>
    public static Color Surface => Of("SurfaceColor");

    /// <summary>
    /// Фон пункта под курсором
    /// </summary>
    public static Color SurfaceRaised => Of("RaisedColor");

    /// <summary>
    /// Линии меню
    /// </summary>
    public static Color Line => Of("LineColor");

    /// <summary>
    /// Основной текст меню
    /// </summary>
    public static Color Text => Of("TextColor");

    /// <summary>
    /// Второстепенный текст меню
    /// </summary>
    public static Color Muted => Of("MutedColor");

    /// <summary>
    /// Цвет работающего процесса
    /// </summary>
    public static Color Ok => Of("OkColor");

    /// <summary>
    /// Цвет сбоя
    /// </summary>
    public static Color Danger => Of("DangerColor");

    /// <summary>
    /// Цвет остановленного процесса
    /// </summary>
    public static Color Idle => Of("IdleColor");

    #endregion Colors

    #region Resources

    /// <summary>
    /// Берёт цвет из ресурсов приложения и переводит его в цвет WinForms
    /// </summary>
    /// <param name="key">Ключ цвета в <c>Themes/Colors.xaml</c></param>
    /// <returns>Цвет для меню и значка</returns>
    private static Color Of(string key)
    {
        var color = (System.Windows.Media.Color)System.Windows.Application.Current.FindResource(key);

        return Color.FromArgb(color.A, color.R, color.G, color.B);
    }

    #endregion Resources
}