using System.Drawing;

namespace Steward.Shell;

/// <summary>
/// Запоминает, где в трее иконка приложения: окна выезжают из этой точки и уходят в неё
/// </summary>
public static class TrayAnchor
{
    /// <summary>
    /// Последняя точка щелчка по иконке в физических пикселях экрана
    /// </summary>
    private static Point? _last;

    /// <summary>
    /// Запоминает точку щелчка по иконке
    /// </summary>
    /// <param name="point">Положение курсора в физических пикселях</param>
    public static void Remember(Point point) => _last = point;

    /// <summary>
    /// Отдаёт запомненную точку, а если иконку ещё не нажимали, правый нижний угол рабочей области
    /// </summary>
    /// <returns>Точка в физических пикселях экрана</returns>
    public static Point Resolve()
    {
        if (_last is { } known)
            return known;

        var area = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);

        return new Point(area.Right - 32, area.Bottom - 8);
    }
}
