using System.Drawing;
using System.Windows.Forms;
using UltimateParser.Tray.Models;

namespace UltimateParser.Tray.Shell;

/// <summary>
/// Цвета выпадающего меню иконки
/// </summary>
public sealed class MenuColors : ProfessionalColorTable
{
    /// <summary>
    /// Фон меню
    /// </summary>
    public override Color ToolStripDropDownBackground => TrayPalette.Surface;

    /// <summary>
    /// Фон пункта под курсором
    /// </summary>
    public override Color MenuItemSelected => TrayPalette.SurfaceRaised;

    /// <summary>
    /// Рамка пункта под курсором
    /// </summary>
    public override Color MenuItemBorder => TrayPalette.SurfaceRaised;

    /// <summary>
    /// Рамка меню
    /// </summary>
    public override Color MenuBorder => TrayPalette.Line;

    /// <summary>
    /// Левое поле меню
    /// </summary>
    public override Color ImageMarginGradientBegin => TrayPalette.Surface;

    /// <summary>
    /// Середина левого поля меню
    /// </summary>
    public override Color ImageMarginGradientMiddle => TrayPalette.Surface;

    /// <summary>
    /// Конец левого поля меню
    /// </summary>
    public override Color ImageMarginGradientEnd => TrayPalette.Surface;

    /// <summary>
    /// Тёмная линия разделителя
    /// </summary>
    public override Color SeparatorDark => TrayPalette.Line;

    /// <summary>
    /// Светлая линия разделителя
    /// </summary>
    public override Color SeparatorLight => TrayPalette.Line;
}
