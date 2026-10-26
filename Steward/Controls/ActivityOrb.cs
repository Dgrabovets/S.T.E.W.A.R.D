using System.Windows;
using System.Windows.Media;
using Steward.Models;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using Point = System.Windows.Point;

namespace Steward.Controls;

/// <summary>
/// Индикатор из точек на сфере. Всегда показывает сферу, вид которой говорит о состоянии задачи или о процессе, а когда процесс закончился, один раз проигрывает завершение
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
    /// Сколько секунд играет завершение
    /// </summary>
    public const double DONE_SECONDS = 1.9;

    /// <summary>
    /// Сколько секунд после окончания процесса задача считается «только что завершённой»: в это время анимация играет, потом сфера спокойная
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
    /// Постоянное случайное число для каждой точки: по нему точки разлетаются и запаздывают
    /// </summary>
    private static readonly double[] Scatter = [.. Enumerable.Range(0, POINT_COUNT).Select(index => Math.Abs(Math.Sin(index * 12.9898) * 43758.5453 % 1))];

    /// <summary>
    /// Состояние индикатора: идёт процесс, закончился или покой
    /// </summary>
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(ActivityStateEnum),
        typeof(ActivityOrb),
        new FrameworkPropertyMetadata(ActivityStateEnum.None, FrameworkPropertyMetadataOptions.AffectsRender, (orb, _) => ((ActivityOrb)orb).UpdateLoop()));

    /// <summary>
    /// Вид сферы
    /// </summary>
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(ActivityKindEnum),
        typeof(ActivityOrb),
        new FrameworkPropertyMetadata(ActivityKindEnum.Working, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Как показывается завершение
    /// </summary>
    public static readonly DependencyProperty DoneStyleProperty = DependencyProperty.Register(
        nameof(DoneStyle),
        typeof(ActivityDoneStyleEnum),
        typeof(ActivityOrb),
        new FrameworkPropertyMetadata(ActivityDoneStyleEnum.Ripple, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Когда процесс закончился: от этого момента идёт анимация. Так она не начинается заново, если карточку пересоздали
    /// </summary>
    public static readonly DependencyProperty SinceProperty = DependencyProperty.Register(
        nameof(Since),
        typeof(DateTimeOffset),
        typeof(ActivityOrb),
        new FrameworkPropertyMetadata(DateTimeOffset.MinValue, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Рисуется каждая n-я точка: у мелких сфер, например в иконке, точек меньше, чтобы они не слипались
    /// </summary>
    public static readonly DependencyProperty StrideProperty = DependencyProperty.Register(
        nameof(Stride),
        typeof(int),
        typeof(ActivityOrb),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Во сколько раз крупнее рисуются точки
    /// </summary>
    public static readonly DependencyProperty DotScaleProperty = DependencyProperty.Register(
        nameof(DotScale),
        typeof(double),
        typeof(ActivityOrb),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Цвет точек
    /// </summary>
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground),
        typeof(Brush),
        typeof(ActivityOrb),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Цвет точек в момент завершения
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
    /// Счётчик кадров: спокойные сферы обновляются через кадр
    /// </summary>
    private int _frame;

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
    /// Вид сферы
    /// </summary>
    public ActivityKindEnum Kind
    {
        get => (ActivityKindEnum)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>
    /// Рисуется каждая n-я точка
    /// </summary>
    public int Stride
    {
        get => (int)GetValue(StrideProperty);
        set => SetValue(StrideProperty, value);
    }

    /// <summary>
    /// Во сколько раз крупнее рисуются точки
    /// </summary>
    public double DotScale
    {
        get => (double)GetValue(DotScaleProperty);
        set => SetValue(DotScaleProperty, value);
    }

    /// <summary>
    /// Как показывается завершение
    /// </summary>
    public ActivityDoneStyleEnum DoneStyle
    {
        get => (ActivityDoneStyleEnum)GetValue(DoneStyleProperty);
        set => SetValue(DoneStyleProperty, value);
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
    /// Цвет точек
    /// </summary>
    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>
    /// Цвет точек в момент завершения
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
        var radius = size / 2 - 1.5;
        var seconds = DateTimeOffset.UtcNow.TimeOfDay.TotalSeconds;
        var age = (DateTimeOffset.UtcNow - Since).TotalSeconds;

        if (State == ActivityStateEnum.Done && age is >= 0 and < DONE_SECONDS)
            DrawDone(drawingContext, center, radius, seconds, age);
        else
            DrawSphere(drawingContext, center, radius, seconds, Kind, ColorOf(Foreground), 1, Math.Max(1, Stride), DotScale);
    }

    /// <summary>
    /// Включает покадровую отрисовку, пока индикатор виден
    /// </summary>
    private void UpdateLoop()
    {
        if (IsLoaded && IsVisible && !_looping)
        {
            CompositionTarget.Rendering += OnFrame;
            _looping = true;
        }
        else if (!(IsLoaded && IsVisible))
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
    /// Перерисовывает кадр. Сферы покоя обновляются через кадр: им плавности хватает, а процессор остаётся свободным
    /// </summary>
    /// <param name="sender">Источник события</param>
    /// <param name="e">Аргументы события</param>
    private void OnFrame(object? sender, EventArgs e)
    {
        var busy = State != ActivityStateEnum.None || Kind is ActivityKindEnum.Working or ActivityKindEnum.Searching or ActivityKindEnum.Connecting or ActivityKindEnum.Composing or ActivityKindEnum.Breathing;
        if (!busy && ++_frame % 2 == 0)
            return;

        InvalidateVisual();
    }

    /// <summary>
    /// Цвет кисти
    /// </summary>
    /// <param name="brush">Кисть</param>
    /// <returns>Цвет сплошной кисти или серый</returns>
    private static Color ColorOf(Brush brush) => brush is SolidColorBrush solid ? solid.Color : Colors.Gray;

    /// <summary>
    /// Кисть для одного уровня глубины: чем ближе точка, тем она плотнее
    /// </summary>
    /// <param name="level">Уровень, 0 — самые дальние точки</param>
    /// <param name="color">Цвет точек</param>
    /// <param name="strength">Общая плотность от 0 до 1</param>
    /// <returns>Замороженная кисть нужной прозрачности</returns>
    private static Brush MakeBrush(int level, Color color, double strength)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)((70 + 185 * level / (DEPTH_LEVELS - 1)) * Math.Clamp(strength, 0, 1)), color.R, color.G, color.B));
        brush.Freeze();

        return brush;
    }

    /// <summary>
    /// Рисует сферу из точек. Движение зависит от вида: у каждого своя дальность точки, яркость, скорость и набор видимых точек
    /// </summary>
    /// <param name="context">Контекст рисования</param>
    /// <param name="center">Центр</param>
    /// <param name="radius">Радиус сферы</param>
    /// <param name="seconds">Время, секунд</param>
    /// <param name="kind">Вид сферы</param>
    /// <param name="color">Цвет точек</param>
    /// <param name="strength">Общая плотность от 0 до 1</param>
    /// <param name="stride">Рисуется каждая n-я точка</param>
    /// <param name="dotScale">Во сколько раз крупнее рисуются точки</param>
    private static void DrawSphere(DrawingContext context, Point center, double radius, double seconds, ActivityKindEnum kind, Color color, double strength, int stride = 1, double dotScale = 1)
    {
        var brushes = Enumerable.Range(0, DEPTH_LEVELS).Select(level => MakeBrush(level, color, strength)).ToArray();
        var (sinTilt, cosTilt) = Math.SinCos(TILT);

        for (var index = 0; index < Points.Length; index++)
        {
            var motion = Motion(kind, seconds, Points[index].Y, Math.Atan2(Points[index].Z, Points[index].X), Scatter[index], index);
            if (!motion.Show || index % stride != 0)
                continue;

            var (x, y, depth) = Rotate(index, seconds * motion.Turn, cosTilt, sinTilt);
            var level = Math.Min(DEPTH_LEVELS - 1, (int)(Math.Clamp(depth + motion.Glow, 0, 0.999) * DEPTH_LEVELS));
            var dot = (0.45 + 0.75 * depth) * (1 + Math.Max(0, motion.Glow) * 0.8) * dotScale;

            context.DrawEllipse(brushes[level], null, new Point(center.X + x * radius * motion.Reach, center.Y + y * radius * motion.Reach), dot, dot);
        }
    }

    /// <summary>
    /// Поворачивает точку сферы и проецирует её на плоскость
    /// </summary>
    /// <param name="index">Номер точки</param>
    /// <param name="turns">Сколько оборотов в секундах нужно пройти: время, умноженное на скорость</param>
    /// <param name="cosTilt">Косинус наклона оси</param>
    /// <param name="sinTilt">Синус наклона оси</param>
    /// <returns>Положение на плоскости от -1 до 1 и глубина от 0 до 1, где 1 ближе к зрителю</returns>
    private static (double X, double Y, double Depth) Rotate(int index, double turns, double cosTilt, double sinTilt)
    {
        var (x, y, z) = Points[index];
        var (sinTurn, cosTurn) = Math.SinCos(turns % TURN_SECONDS / TURN_SECONDS * Math.PI * 2);
        var turnedX = x * cosTurn + z * sinTurn;
        var turnedZ = -x * sinTurn + z * cosTurn;

        return (turnedX, y * cosTilt - turnedZ * sinTilt, (y * sinTilt + turnedZ * cosTilt + 1) / 2);
    }

    /// <summary>
    /// Считает, как ведёт себя точка выбранного вида в этот момент
    /// </summary>
    /// <param name="kind">Вид сферы</param>
    /// <param name="seconds">Время, секунд</param>
    /// <param name="height">Высота точки на единичной сфере от -1 до 1</param>
    /// <param name="azimuth">Угол точки вокруг вертикальной оси, радианы</param>
    /// <param name="scatter">Постоянное случайное число точки от 0 до 1</param>
    /// <param name="index">Номер точки</param>
    /// <returns>Дальность от центра в долях радиуса, добавка яркости, скорость вращения и показывается ли точка</returns>
    private static (double Reach, double Glow, double Turn, bool Show) Motion(ActivityKindEnum kind, double seconds, double height, double azimuth, double scatter, int index)
    {
        switch (kind)
        {
            case ActivityKindEnum.Searching:
                return (1, Math.Max(0, 1 - Math.Abs(height - ((seconds * 0.7 % 1) * 2.6 - 1.3)) * 3.2) * 0.6, 1, true);

            case ActivityKindEnum.Connecting:
                return (0.62 + 0.38 * (0.5 + 0.5 * Math.Sin(seconds * 2.4 - height * 3)), 0, 1, true);

            case ActivityKindEnum.Composing:
                var phase = seconds % 3.2 / 3.2;
                var gather = phase < 0.7 ? 1 - Math.Pow(1 - phase / 0.7, 3) : phase < 0.9 ? 1 : 1 - (phase - 0.9) / 0.1;

                return (1 + (1 - gather) * (0.5 + scatter), gather > 0.98 ? 0.15 : 0, 1, true);

            case ActivityKindEnum.Breathing:
                return (0.82 + 0.18 * Math.Sin(seconds * 1.3), 0, 0.5, true);

            case ActivityKindEnum.Alive:
                return (1 + 0.03 * Math.Sin(seconds * 1.5 + height * 4), 0.12 * Math.Max(0, Math.Sin(seconds * 2 + index)), 0.6, true);

            case ActivityKindEnum.Calm:
                return (1, 0.15 * Math.Max(0, Math.Sin(seconds * 0.9 - height * 2)), 0.25, true);

            case ActivityKindEnum.Dormant:
                return (0.92, -0.05, 0.2, index % 3 == 0);

            case ActivityKindEnum.Attention:
                return (0.8 + 0.2 * Math.Sin(seconds * 3.2), 0.2 * Math.Max(0, Math.Sin(seconds * 3.2)), 0.7, true);

            case ActivityKindEnum.Failed:
                var wobble = 0.5 + 0.5 * Math.Sin(seconds * 5 + index);

                return (1 + (scatter - 0.5) * 0.35 * wobble + (scatter > 0.85 ? 0.45 : 0), 0, 0.4, true);

            case ActivityKindEnum.Waiting:
                return (0.9 + 0.06 * Math.Sin(seconds * 0.7), -0.12, 0.12, index % 2 == 0);

            case ActivityKindEnum.Sealed:
                var line = Math.Abs(height * 5 - Math.Round(height * 5)) < 0.32 && Math.Abs(height) < 0.85;

                return (1, line ? 0.3 : 0, 0.3, line);

            case ActivityKindEnum.Shattered:
                var drift = (seconds * 0.35 + scatter) % 1;
                var shard = scatter > 0.72;

                return (shard ? 1 + 0.8 * drift : 1, shard ? -0.6 * drift : 0, 0.3, true);

            case ActivityKindEnum.Sweep:
                var behind = (((seconds * 1.1 - azimuth / (Math.PI * 2)) % 1) + 1) % 1;

                return (1, Math.Max(0, 0.7 - behind * 2.2), 0, true);

            case ActivityKindEnum.Hollow:
                return (1, 0.3, 0.5, Math.Abs(height) < 0.12);

            case ActivityKindEnum.Beacon:
                var belt = Math.Abs(height) < 0.14;

                return (belt ? 1.3 : 0.78, belt ? 0.35 : 0, belt ? 2.4 : 0.8, true);

            default:
                return (1, 0, 1, true);
        }
    }

    /// <summary>
    /// Проигрывает завершение выбранным способом
    /// </summary>
    /// <param name="context">Контекст рисования</param>
    /// <param name="center">Центр</param>
    /// <param name="radius">Радиус сферы</param>
    /// <param name="seconds">Время, секунд</param>
    /// <param name="age">Сколько секунд прошло с окончания процесса</param>
    private void DrawDone(DrawingContext context, Point center, double radius, double seconds, double age)
    {
        var done = ColorOf(DoneBrush);
        switch (DoneStyle)
        {
            case ActivityDoneStyleEnum.DotTick:
                DrawDotTick(context, center, radius, seconds, age, done);
                break;
            case ActivityDoneStyleEnum.Burst:
                DrawBurst(context, center, radius, seconds, age, done);
                break;
            default:
                DrawRipple(context, center, radius, seconds, age, done);
                break;
        }
    }

    /// <summary>
    /// Сфера вспыхивает цветом завершения, от неё расходится кольцо из точек и тает
    /// </summary>
    /// <param name="context">Контекст рисования</param>
    /// <param name="center">Центр</param>
    /// <param name="radius">Радиус сферы</param>
    /// <param name="seconds">Время, секунд</param>
    /// <param name="age">Секунд с окончания</param>
    /// <param name="done">Цвет завершения</param>
    private void DrawRipple(DrawingContext context, Point center, double radius, double seconds, double age, Color done)
    {
        var share = age / DONE_SECONDS;
        var wave = Math.Clamp(age / 1.2, 0, 1);
        var calm = ColorOf(Foreground);
        var mixed = Mix(done, calm, Math.Clamp((age - 0.9) / 1, 0, 1));

        DrawSphere(context, center, radius * (1 + 0.1 * Math.Sin(Math.Min(share * 2, 1) * Math.PI)), seconds, ActivityKindEnum.Calm, mixed, 1);
        if (wave >= 1)
            return;

        var spread = radius * (1 + 0.9 * (1 - Math.Pow(1 - wave, 3)));
        var fade = 1 - wave;
        var brush = new SolidColorBrush(Color.FromArgb((byte)(255 * fade), done.R, done.G, done.B));
        brush.Freeze();
        for (var index = 0; index < 36; index++)
        {
            var (sin, cos) = Math.SinCos(index / 36.0 * Math.PI * 2);
            context.DrawEllipse(brush, null, new Point(center.X + cos * spread, center.Y + sin * spread), 1.1 * fade + 0.4, 1.1 * fade + 0.4);
        }
    }

    /// <summary>
    /// Точки собираются в кольцо с галочкой, держатся и возвращаются в сферу
    /// </summary>
    /// <param name="context">Контекст рисования</param>
    /// <param name="center">Центр</param>
    /// <param name="radius">Радиус сферы</param>
    /// <param name="seconds">Время, секунд</param>
    /// <param name="age">Секунд с окончания</param>
    /// <param name="done">Цвет завершения</param>
    private void DrawDotTick(DrawingContext context, Point center, double radius, double seconds, double age, Color done)
    {
        var (sinTilt, cosTilt) = Math.SinCos(TILT);
        var brush = new SolidColorBrush(done);
        brush.Freeze();
        var ringCount = POINT_COUNT * 6 / 10;

        for (var index = 0; index < POINT_COUNT; index++)
        {
            var (x, y, depth) = Rotate(index, seconds * 0.25, cosTilt, sinTilt);
            var from = new Point(center.X + x * radius, center.Y + y * radius);
            var to = TickTarget(index, ringCount, center, radius);
            var delay = Scatter[index] * 0.25;
            var gather = Ease(Math.Clamp((age - delay) / 0.5, 0, 1));
            var release = Ease(Math.Clamp((age - 1.2 - delay) / 0.5, 0, 1));
            var at = gather * (1 - release);
            var dot = (0.45 + 0.75 * depth) * (1 - at) + 1.25 * at;

            context.DrawEllipse(at > 0.5 ? brush : MakeBrush(Math.Min(DEPTH_LEVELS - 1, (int)(depth * DEPTH_LEVELS)), done, 1), null, new Point(from.X + (to.X - from.X) * at, from.Y + (to.Y - from.Y) * at), dot, dot);
        }
    }

    /// <summary>
    /// Место точки в кольце с галочкой: первые точки идут по кольцу, остальные по двум отрезкам галочки
    /// </summary>
    /// <param name="index">Номер точки</param>
    /// <param name="ringCount">Сколько точек отдано кольцу</param>
    /// <param name="center">Центр</param>
    /// <param name="radius">Радиус сферы</param>
    /// <returns>Точка на плоскости</returns>
    private static Point TickTarget(int index, int ringCount, Point center, double radius)
    {
        if (index < ringCount)
        {
            var (sin, cos) = Math.SinCos(index / (double)ringCount * Math.PI * 2 - Math.PI / 2);

            return new Point(center.X + cos * radius, center.Y + sin * radius);
        }

        var share = (index - ringCount) / (double)(POINT_COUNT - ringCount - 1);
        var from = new Point(center.X - radius * 0.42, center.Y + radius * 0.02);
        var corner = new Point(center.X - radius * 0.1, center.Y + radius * 0.36);
        var to = new Point(center.X + radius * 0.46, center.Y - radius * 0.3);

        return share < 0.4 ? Lerp(from, corner, share / 0.4) : Lerp(corner, to, (share - 0.4) / 0.6);
    }

    /// <summary>
    /// Сфера схлопывается в яркую точку, взрывается и точки возвращаются на места
    /// </summary>
    /// <param name="context">Контекст рисования</param>
    /// <param name="center">Центр</param>
    /// <param name="radius">Радиус сферы</param>
    /// <param name="seconds">Время, секунд</param>
    /// <param name="age">Секунд с окончания</param>
    /// <param name="done">Цвет завершения</param>
    private void DrawBurst(DrawingContext context, Point center, double radius, double seconds, double age, Color done)
    {
        var (sinTilt, cosTilt) = Math.SinCos(TILT);
        var implode = Math.Clamp(age / 0.4, 0, 1);
        var explode = Math.Clamp((age - 0.4) / 1.3, 0, 1);
        var reach = age < 0.4 ? 1 - 0.85 * Ease(implode) : 0.15 + 1.25 * (1 - Math.Pow(1 - explode, 3));
        var settle = Math.Clamp((age - 1.1) / 0.8, 0, 1);
        reach = reach * (1 - settle) + settle;
        var brushes = Enumerable.Range(0, DEPTH_LEVELS).Select(level => MakeBrush(level, Mix(done, ColorOf(Foreground), settle), 1)).ToArray();

        for (var index = 0; index < POINT_COUNT; index++)
        {
            var (x, y, depth) = Rotate(index, seconds * 0.25, cosTilt, sinTilt);
            var level = Math.Min(DEPTH_LEVELS - 1, (int)(Math.Clamp(depth + (1 - settle) * 0.4, 0, 0.999) * DEPTH_LEVELS));
            var dot = (0.45 + 0.75 * depth) * (1 + (1 - settle) * 0.6);

            context.DrawEllipse(brushes[level], null, new Point(center.X + x * radius * reach, center.Y + y * radius * reach), dot, dot);
        }
    }

    /// <summary>
    /// Плавное ускорение и замедление
    /// </summary>
    /// <param name="value">Доля пути от 0 до 1</param>
    /// <returns>Сглаженная доля пути</returns>
    private static double Ease(double value) => value * value * (3 - 2 * value);

    /// <summary>
    /// Смесь двух цветов
    /// </summary>
    /// <param name="first">Цвет при нуле</param>
    /// <param name="second">Цвет при единице</param>
    /// <param name="share">Доля второго цвета</param>
    /// <returns>Промежуточный цвет</returns>
    private static Color Mix(Color first, Color second, double share) => Color.FromRgb(
        (byte)(first.R + (second.R - first.R) * share),
        (byte)(first.G + (second.G - first.G) * share),
        (byte)(first.B + (second.B - first.B) * share));

    /// <summary>
    /// Точка на отрезке между двумя
    /// </summary>
    /// <param name="from">Начало</param>
    /// <param name="to">Конец</param>
    /// <param name="share">Доля пути</param>
    /// <returns>Точка на нужной доле отрезка</returns>
    private static Point Lerp(Point from, Point to, double share) => new(from.X + (to.X - from.X) * share, from.Y + (to.Y - from.Y) * share);
}