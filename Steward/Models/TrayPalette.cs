namespace Steward.Models;

/// <summary>
/// Цвета значка в трее. Окно берёт палитру из разметки
/// </summary>
public static class TrayPalette
{
    #region Colors

    /// <summary>
    /// Фон меню
    /// </summary>
    public static readonly Color Surface = Color.FromArgb(42, 34, 72);

    /// <summary>
    /// Фон пункта под курсором
    /// </summary>
    public static readonly Color SurfaceRaised = Color.FromArgb(56, 46, 94);

    /// <summary>
    /// Линии меню
    /// </summary>
    public static readonly Color Line = Color.FromArgb(74, 62, 118);

    /// <summary>
    /// Основной текст меню
    /// </summary>
    public static readonly Color Text = Color.FromArgb(244, 240, 252);

    /// <summary>
    /// Второстепенный текст меню
    /// </summary>
    public static readonly Color Muted = Color.FromArgb(176, 164, 206);

    /// <summary>
    /// Зелёный цвет работающего процесса
    /// </summary>
    public static readonly Color Ok = Color.FromArgb(74, 214, 154);

    /// <summary>
    /// Цвет сбоя
    /// </summary>
    public static readonly Color Danger = Color.FromArgb(255, 122, 146);

    /// <summary>
    /// Серо-фиолетовый цвет остановленного процесса
    /// </summary>
    public static readonly Color Idle = Color.FromArgb(132, 122, 158);

    #endregion Colors
}
