using System.Drawing.Drawing2D;

namespace UltimateParser.Tray.Tray.Models;

/// <summary>
/// Цвета значка в трее. Окно берёт палитру из разметки
/// </summary>
public static class TrayPalette
{
    #region Colors

    /// <summary>
    /// Фон меню
    /// </summary>
    public static readonly System.Drawing.Color Surface = System.Drawing.Color.FromArgb(42, 34, 72);

    /// <summary>
    /// Фон пункта под курсором
    /// </summary>
    public static readonly System.Drawing.Color SurfaceRaised = System.Drawing.Color.FromArgb(56, 46, 94);

    /// <summary>
    /// Линии меню
    /// </summary>
    public static readonly System.Drawing.Color Line = System.Drawing.Color.FromArgb(74, 62, 118);

    /// <summary>
    /// Основной текст меню
    /// </summary>
    public static readonly System.Drawing.Color Text = System.Drawing.Color.FromArgb(244, 240, 252);

    /// <summary>
    /// Второстепенный текст меню
    /// </summary>
    public static readonly System.Drawing.Color Muted = System.Drawing.Color.FromArgb(176, 164, 206);

    /// <summary>
    /// Зелёный цвет работающего процесса
    /// </summary>
    public static readonly System.Drawing.Color Ok = System.Drawing.Color.FromArgb(74, 214, 154);

    /// <summary>
    /// Цвет сбоя
    /// </summary>
    public static readonly System.Drawing.Color Danger = System.Drawing.Color.FromArgb(255, 122, 146);

    /// <summary>
    /// Серо-фиолетовый цвет остановленного процесса
    /// </summary>
    public static readonly System.Drawing.Color Idle = System.Drawing.Color.FromArgb(132, 122, 158);

    #endregion Colors

    #region Shape

    /// <summary>
    /// Строит скруглённый контур значка
    /// </summary>
    /// <param name="bounds">
    /// Прямоугольник контура
    /// </param>
    /// <param name="radius">
    /// Радиус угла в пикселях
    /// </param>
    /// <returns>
    /// Контур, который нужно освободить
    /// </returns>
    public static GraphicsPath Rounded(System.Drawing.Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        var arc = new System.Drawing.Rectangle(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    #endregion Shape
}
