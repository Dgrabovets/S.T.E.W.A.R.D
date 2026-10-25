namespace UltimateParser.Tray.Models;

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
}
