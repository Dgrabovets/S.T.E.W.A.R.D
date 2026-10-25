using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Steward.Models;

namespace Steward.Controls;

/// <summary>
/// Индикатор процесса. Пока процесс идёт, вращается сфера из точек, когда закончился, рисуется кольцо с галочкой, в покое показывает точку состояния с кольцом
/// </summary>
public sealed class ActivityOrb : FrameworkElement
{
    /// <summary>
    /// Сколько точек на сфере
    /// </summary>
    private const int POINT_COUNT = 150;

    /// <summary>
    /// Число уровней глубины: точки одного уровня рисуются одной кистью
    /// </summary>
    private const int DEPTH_LEVELS = 6;

    /// <summary>
    /// Секунд на полный оборот сферы
    /// </summary>
    private const double TURN_SECONDS = 7;

    /// <summary>
    /// Наклон оси вращения, в радианах
    /// </summary>
    private const double TILT = 0.4;

    /// <summary>
    /// Сколько секунд рисуется кольцо
    /// </summary>
    private const double RING_SECONDS = 0.45;

    /// <summary>
    /// Сколько секунд рисуется галочка после кольца
    /// </summary>
    private const double TICK_SECONDS = 0.35;

    /// <summary>
    /// Сколько секунд галочка остаётся на экране, потом снова точка
    /// </summary>
    public const double DONE_SHOW_SECONDS = 6;

    /// <summary>
    /// Точки сферы единичного радиуса, равномерно по спирали Фибоначчи
    /// </summary>
    private static readonly (double X, double Y, double Z)[] Points = [.. Enumerable.Range(0, POINT_COUNT).Select(index =>
    {
        var y = 1 - (index + 0.5) * 2 / POINT_COUNT;
        var ring = Math.Sqrt(1 - y * y);
        var angle = index * Math.PI * (3 - Math.Sqrt(5));

        return (Math.Cos(angle) * ring, y, Math.Sin(angle) * ring);
    })];

    /// <summary>
    /// Постоянное случайное число для каждой точки: по нему точки разлетаются при сборке сферы
    /// </summary>
    private static readonly double[] Scatter = [.. Enumerable.Range(0, POINT_COUNT).Select(index => (Math.Sin(index * 12.9898) * 43758.5453) % 1 is var value ? Math.Abs(value) : 0)];

    /// <summary>
    /// Состояние индикатора
    /// </summary>
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(ActivityStateEnum),
        typeof(ActivityOrb),
        new FrameworkPropertyMetadata(ActivityStateEnum.None, FrameworkPropertyMetadataOptions.AffectsRender, (orb, _) => ((ActivityOrb)orb).UpdateLoop()));

    /// <summary>
    /// Как движутся точки, пока процесс идёт
    /// </summary>
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(ActivityKindEnum),
        typeof(ActivityOrb),
        new FrameworkPropertyMetadata(ActivityKindEnum.Working, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Когда процесс закончился: от этого момента идёт анимация галочки. Так анимация не начинается заново, если карточку пересоздали
    /// </summary>
    public static readonly DependencyProperty SinceProperty = DependencyProperty.Register(
        nameof(Since),
        typeof(DateTimeOffset),
        typeof(ActivityOrb),
        new FrameworkPropertyMetadata(DateTimeOffset.MinValue, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Цвет точек и кольца в покое и во время процесса
    /// </summary>
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground),
        typeof(Brush),
        typeof(ActivityOrb),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Цвет галочки и её кольца
    /// </summary>
    public static readonly DependencyProperty DoneBrushProperty = DependencyProperty.Register(
        nameof(DoneBrush),
        typeof(Brush),
        typeof(ActivityOrb),
        new FrameworkPropertyMetadata(Brushes.Green, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Идёт ли покадровая отрисовка
    /// </summary>
    private bool _looping;

    /// <summary>
    /// Создаёт индикатор и подписывается на показ и скрытие, чтобы не рисовать кадры невидимому окну
    /// </summary>
    public ActivityOrb()
    {
        IsHitTestVisible = false;
        SnapsToDevicePixels = false;
        Loaded += (_, _) => UpdateLoop();
        Unloaded += (_, _) => StopLoop();
        IsVisibleChanged += (_, _) => UpdateLoop();
    }

    /// <summary>
    /// Состояние индикатора
    /// </summary>
    public ActivityStateEnum State
    {
        get => (ActivityStateEnum)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>
    /// Как движутся точки, пока процесс идёт
    /// </summary>
    public ActivityKindEnum Kind
    {
        get => (ActivityKindEnum)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>
    /// Когда процесс закончился
    /// </summary>
    public DateTimeOffset Since
    {
        get => (DateTimeOffset)GetValue(SinceProperty);
        set => SetValue(SinceProperty, value);
    }

    /// <summary>
    /// Цвет точек и кольца
    /// </summary>
    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>
    /// Цвет галочки
    /// </summary>
    public Brush DoneBrush
    {
        get => (Brush)GetValue(DoneBrushProperty);
        set => SetValue(DoneBrushProperty, value);
    }

    /// <summary>
    /// Рисует текущий кадр
    /// </summary>
    /// <param name="drawingContext">Контекст рисования</param>
    protected override void OnRender(DrawingContext drawingContext)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0)
            return;

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var doneAge = (DateTimeOffset.UtcNow - Since).TotalSeconds;

        if (State == ActivityStateEnum.Working)
            DrawSphere(drawingContext, center, size / 2 - 1);
        else if (State == ActivityStateEnum.Done && doneAge is >= 0 and < DONE_SHOW_SECONDS)
            DrawDone(drawingContext, center, size / 2 - 1.5, doneAge);
        else
            DrawDot(drawingContext, center, size);
    }

    /// <summary>
    /// Включает покадровую отрисовку, пока она нужна: идёт процесс или играет галочка
    /// </summary>
    private void UpdateLoop()
    {
        var needed = IsLoaded && IsVisible && State != ActivityStateEnum.None;
        if (needed && !_looping)
        {
            CompositionTarget.Rendering += OnFrame;
            _looping = true;
        }
        else if (!needed)
            StopLoop();

        InvalidateVisual();
    }

    /// <summary>
    /// Останавливает покадровую отрисовку
    /// </summary>
    private void StopLoop()
    {
        if (!_looping)
            return;

        CompositionTarget.Rendering -= OnFrame;
        _looping = false;
    }

    /// <summary>
    /// Просит перерисовать кадр. Когда галочка отыграла своё, возвращает точку и останавливает отрисовку
    /// </summary>
    /// <param name="sender">Источник события</param>
    /// <param name="e">Аргументы события</param>
    private void OnFrame(object? sender, EventArgs e)
    {
        if (State == ActivityStateEnum.Done && (DateTimeOffset.UtcNow - Since).TotalSeconds >= DONE_SHOW_SECONDS)
        {
            StopLoop();
            InvalidateVisual();

            return;
        }

        InvalidateVisual();
    }

    /// <summary>
    /// Рисует точку состояния: заливка и тонкое кольцо
    /// </summary>
    /// <param name="context">Контекст рисования</param>
    /// <param name="center">Центр</param>
    /// <param name="size">Размер индикатора</param>
    private void DrawDot(DrawingContext context, Point center, double size)
    {
        var ring = Math.Min(size, 16) / 2;
        context.DrawEllipse(null, new Pen(Foreground, 1), center, ring - 0.5, ring - 0.5);
        context.DrawEllipse(Foreground, null, center, ring - 3.5, ring - 3.5);
    }

    /// <summary>
    /// Рисует сферу из точек. Движение зависит от вида: у каждого свои радиус точки, яркость и размер
    /// </summary>
    /// <param name="context">Контекст рисования</param>
    /// <param name="center">Центр</param>
    /// <param name="radius">Радиус сферы</param>
    private void DrawSphere(DrawingContext context, Point center, double radius)
    {
        var seconds = DateTimeOffset.UtcNow.TimeOfDay.TotalSeconds;
        var kind = Kind;
        var turn = seconds % TURN_SECONDS / TURN_SECONDS * Math.PI * 2 * (kind == ActivityKindEnum.Breathing ? 0.5 : 1);
        var (sinTurn, cosTurn) = Math.SinCos(turn);
        var (sinTilt, cosTilt) = Math.SinCos(TILT);
        var brushes = Enumerable.Range(0, DEPTH_LEVELS).Select(MakeBrush).ToArray();

        for (var index = 0; index < Points.Length; index++)
        {
            var (x, y, z) = Points[index];
            var turnedX = x * cosTurn + z * sinTurn;
            var turnedZ = -x * sinTurn + z * cosTurn;
            var tiltedY = y * cosTilt - turnedZ * sinTilt;
            var depth = (y * sinTilt + turnedZ * cosTilt + 1) / 2;
            var (reach, glow) = Motion(kind, seconds, y, Scatter[index]);
            var level = Math.Min(DEPTH_LEVELS - 1, (int)(Math.Clamp(depth + glow, 0, 0.999) * DEPTH_LEVELS));
            var dot = (0.45 + 0.75 * depth) * (1 + glow * 0.8);

            context.DrawEllipse(brushes[level], null, new Point(center.X + turnedX * radius * reach, center.Y + tiltedY * radius * reach), dot, dot);
        }
    }

    /// <summary>
    /// Считает, как далеко от центра лежит точка и насколько она подсвечена в этот момент
    /// </summary>
    /// <param name="kind">Вид движения</param>
    /// <param name="seconds">Время, секунд</param>
    /// <param name="height">Высота точки на единичной сфере от -1 до 1</param>
    /// <param name="scatter">Постоянное случайное число точки от 0 до 1</param>
    /// <returns>Доля радиуса сферы и добавка яркости от 0 до 1</returns>
    private static (double Reach, double Glow) Motion(ActivityKindEnum kind, double seconds, double height, double scatter)
    {
        switch (kind)
        {
            case ActivityKindEnum.Searching:
                var band = (seconds * 0.7 % 1) * 2.6 - 1.3;

                return (1, Math.Max(0, 1 - Math.Abs(height - band) * 3.2) * 0.6);

            case ActivityKindEnum.Connecting:
                return (0.62 + 0.38 * (0.5 + 0.5 * Math.Sin(seconds * 2.4 - height * 3)), 0);

            case ActivityKindEnum.Composing:
                var phase = seconds % 3.2 / 3.2;
                var gather = phase < 0.7 ? 1 - Math.Pow(1 - phase / 0.7, 3) : phase < 0.9 ? 1 : 1 - (phase - 0.9) / 0.1;

                return (1 + (1 - gather) * (0.5 + scatter), gather > 0.98 ? 0.15 : 0);

            case ActivityKindEnum.Breathing:
                return (0.82 + 0.18 * Math.Sin(seconds * 1.3), 0);

            default:
                return (1, 0);
        }
    }

    /// <summary>
    /// Кисть для одного уровня глубины: чем ближе точка, тем она плотнее
    /// </summary>
    /// <param name="level">Уровень, 0 — самые дальние точки</param>
    /// <returns>Замороженная кисть цвета индикатора с нужной прозрачностью</returns>
    private Brush MakeBrush(int level)
    {
        var color = Foreground is SolidColorBrush solid ? solid.Color : Colors.Gray;
        var brush = new SolidColorBrush(Color.FromArgb((byte)(70 + 185 * level / (DEPTH_LEVELS - 1)), color.R, color.G, color.B));
        brush.Freeze();

        return brush;
    }

    /// <summary>
    /// Рисует кольцо, которое замыкается, и галочку внутри него
    /// </summary>
    /// <param name="context">Контекст рисования</param>
    /// <param name="center">Центр</param>
    /// <param name="radius">Радиус кольца</param>
    /// <param name="age">Сколько секунд прошло с окончания процесса</param>
    private void DrawDone(DrawingContext context, Point center, double radius, double age)
    {
        var pen = new Pen(DoneBrush, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        var ring = Math.Clamp(age / RING_SECONDS, 0, 1);
        var tick = Math.Clamp((age - RING_SECONDS) / TICK_SECONDS, 0, 1);

        if (ring >= 1)
            context.DrawEllipse(null, pen, center, radius, radius);
        else
        {
            var start = new Point(center.X, center.Y - radius);
            var (sin, cos) = Math.SinCos(ring * Math.PI * 2);
            var end = new Point(center.X + radius * sin, center.Y - radius * cos);
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(start, false, false);
                ctx.ArcTo(end, new Size(radius, radius), 0, ring > 0.5, SweepDirection.Clockwise, true, false);
            }

            geometry.Freeze();
            context.DrawGeometry(null, pen, geometry);
        }

        if (tick <= 0)
            return;

        var from = new Point(center.X - radius * 0.42, center.Y + radius * 0.02);
        var corner = new Point(center.X - radius * 0.1, center.Y + radius * 0.36);
        var to = new Point(center.X + radius * 0.46, center.Y - radius * 0.3);
        var firstShare = Math.Clamp(tick / 0.4, 0, 1);
        var secondShare = Math.Clamp((tick - 0.4) / 0.6, 0, 1);

        context.DrawLine(pen, from, Lerp(from, corner, firstShare));
        if (secondShare > 0)
            context.DrawLine(pen, corner, Lerp(corner, to, secondShare));
    }

    /// <summary>
    /// Точка на отрезке между двумя
    /// </summary>
    /// <param name="from">Начало</param>
    /// <param name="to">Конец</param>
    /// <param name="share">Доля пути от 0 до 1</param>
    /// <returns>Точка на нужной доле отрезка</returns>
    private static Point Lerp(Point from, Point to, double share) => new(from.X + (to.X - from.X) * share, from.Y + (to.Y - from.Y) * share);
}