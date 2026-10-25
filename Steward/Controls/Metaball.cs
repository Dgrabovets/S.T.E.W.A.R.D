using System.Windows;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace Steward.Controls;

/// <summary>
/// Перемычка между двумя каплями: когда круги близко, между ними вытягивается «шейка», как у жидкости
/// </summary>
public static class Metaball
{
    /// <summary>
    /// Насколько далеко от касательных к кругам идут точки перемычки, от 0 до 1
    /// </summary>
    private const double SPREAD = 0.5;

    /// <summary>
    /// Длина ручек кривых, чем больше, тем мягче шейка
    /// </summary>
    private const double HANDLE = 2.4;

    /// <summary>
    /// Во сколько раз сумма радиусов расстояние, дальше которого перемычки нет
    /// </summary>
    private const double REACH = 1.3;

    /// <summary>
    /// Строит перемычку между двумя кругами
    /// </summary>
    /// <param name="first">Центр первого круга</param>
    /// <param name="firstRadius">Радиус первого круга</param>
    /// <param name="second">Центр второго круга</param>
    /// <param name="secondRadius">Радиус второго круга</param>
    /// <returns>Замкнутая фигура перемычки или <see langword="null"/>, если круги слишком далеко или один лежит внутри другого</returns>
    public static Geometry? Bridge(Point first, double firstRadius, Point second, double secondRadius)
    {
        var distance = (second - first).Length;
        if (firstRadius <= 0 || secondRadius <= 0 || distance > (firstRadius + secondRadius) * REACH || distance <= Math.Abs(firstRadius - secondRadius))
            return null;

        var overlap1 = 0.0;
        var overlap2 = 0.0;
        if (distance < firstRadius + secondRadius)
        {
            overlap1 = Math.Acos(Math.Clamp((firstRadius * firstRadius + distance * distance - secondRadius * secondRadius) / (2 * firstRadius * distance), -1, 1));
            overlap2 = Math.Acos(Math.Clamp((secondRadius * secondRadius + distance * distance - firstRadius * firstRadius) / (2 * secondRadius * distance), -1, 1));
        }

        var between = Math.Atan2(second.Y - first.Y, second.X - first.X);
        var spread = Math.Acos(Math.Clamp((firstRadius - secondRadius) / distance, -1, 1));
        var angle1 = between + overlap1 + (spread - overlap1) * SPREAD;
        var angle2 = between - overlap1 - (spread - overlap1) * SPREAD;
        var angle3 = between + Math.PI - overlap2 - (Math.PI - overlap2 - spread) * SPREAD;
        var angle4 = between - Math.PI + overlap2 + (Math.PI - overlap2 - spread) * SPREAD;

        var firstTop = OnCircle(first, firstRadius, angle1);
        var firstBottom = OnCircle(first, firstRadius, angle2);
        var secondTop = OnCircle(second, secondRadius, angle3);
        var secondBottom = OnCircle(second, secondRadius, angle4);

        var handleScale = Math.Min(SPREAD * HANDLE, (secondTop - firstTop).Length / (firstRadius + secondRadius)) * Math.Min(1, distance * 2 / (firstRadius + secondRadius));
        var firstHandle = firstRadius * handleScale;
        var secondHandle = secondRadius * handleScale;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(firstTop, true, true);
            context.BezierTo(
                firstTop + Direction(angle1 - Math.PI / 2) * firstHandle,
                secondTop + Direction(angle3 + Math.PI / 2) * secondHandle,
                secondTop,
                true,
                false);
            context.LineTo(secondBottom, true, false);
            context.BezierTo(
                secondBottom + Direction(angle4 - Math.PI / 2) * secondHandle,
                firstBottom + Direction(angle2 + Math.PI / 2) * firstHandle,
                firstBottom,
                true,
                false);
        }

        geometry.Freeze();

        return geometry;
    }

    /// <summary>
    /// Точка на окружности
    /// </summary>
    /// <param name="center">Центр</param>
    /// <param name="radius">Радиус</param>
    /// <param name="angle">Угол, радианы</param>
    /// <returns>Точка на окружности</returns>
    private static Point OnCircle(Point center, double radius, double angle) => center + Direction(angle) * radius;

    /// <summary>
    /// Единичный вектор по углу
    /// </summary>
    /// <param name="angle">Угол, радианы</param>
    /// <returns>Вектор длины один</returns>
    private static Vector Direction(double angle) => new(Math.Cos(angle), Math.Sin(angle));
}