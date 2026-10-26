using System.Windows;
using System.Windows.Media;

namespace Steward.Controls;

/// <summary>
/// Горизонтальный ползунок интервала в цветах панели. Бегунок «жидкий»: следует за значением с запаздыванием, от него тянется капля-хвост и между ними вытягивается шейка, как у жидкости
/// </summary>
public sealed class IntervalBar : FrameworkElement
{
    /// <summary>
    /// Нижняя граница, в минутах
    /// </summary>
    public const double MIN_VALUE = 1;

    /// <summary>
    /// Верхняя граница, в минутах
    /// </summary>
    public const double MAX_VALUE = 180;

    /// <summary>
    /// Высота дорожки
    /// </summary>
    private const double TRACK_HEIGHT = 6;

    /// <summary>
    /// Радиус бегунка
    /// </summary>
    private const double THUMB_RADIUS = 11;

    /// <summary>
    /// Радиус свечения вокруг бегунка
    /// </summary>
    private const double GLOW_RADIUS = 16;

    /// <summary>
    /// Жёсткость пружины бегунка: он догоняет значение быстро
    /// </summary>
    private const double THUMB_STIFFNESS = 520;

    /// <summary>
    /// Затухание пружины бегунка
    /// </summary>
    private const double THUMB_DAMPING = 38;

    /// <summary>
    /// Жёсткость пружины хвоста: он отстаёт и покачивается
    /// </summary>
    private const double TAIL_STIFFNESS = 150;

    /// <summary>
    /// Затухание пружины хвоста: чем меньше, тем сильнее покачивание
    /// </summary>
    private const double TAIL_DAMPING = 11;

    /// <summary>
    /// Самый крупный шаг расчёта пружин, секунд
    /// </summary>
    private const double MAX_STEP = 1.0 / 120;

    /// <summary>
    /// Где сейчас бегунок на экране
    /// </summary>
    private double _thumb = double.NaN;

    /// <summary>
    /// Скорость бегунка
    /// </summary>
    private double _thumbSpeed;

    /// <summary>
    /// Где сейчас хвост
    /// </summary>
    private double _tail;

    /// <summary>
    /// Скорость хвоста
    /// </summary>
    private double _tailSpeed;

    /// <summary>
    /// Когда считали пружины в последний раз
    /// </summary>
    private DateTime _lastStep = DateTime.UtcNow;

    /// <summary>
    /// Идёт ли покадровый расчёт
    /// </summary>
    private bool _looping;

    /// <summary>
    /// Выбранное число минут
    /// </summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(IntervalBar),
        new FrameworkPropertyMetadata(MIN_VALUE, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    /// <summary>
    /// Создаёт горизонтальную полосу
    /// </summary>
    public IntervalBar()
    {
        Height = 32;
        Cursor = System.Windows.Input.Cursors.Hand;
        Focusable = true;
        SnapsToDevicePixels = true;
    }

    /// <summary>
    /// Выбранное число минут
    /// </summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Перерисовывает полосу, когда число изменилось снаружи
    /// </summary>
    /// <param name="element">Эта полоса</param>
    /// <param name="args">Старое и новое значение</param>
    private static void OnValueChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        var bar = (IntervalBar)element;
        bar.StartLoop();
        bar.InvalidateVisual();
    }

    /// <summary>
    /// Ставит бегунок и хвост в заданные места без анимации: для снимков и тестов
    /// </summary>
    /// <param name="thumb">Горизонталь бегунка</param>
    /// <param name="tail">Горизонталь хвоста</param>
    public void Pose(double thumb, double tail)
    {
        _thumb = thumb;
        _tail = tail;
        _thumbSpeed = _tailSpeed = 0;
        InvalidateVisual();
    }

    /// <summary>
    /// Где бегунок должен стоять для текущего значения
    /// </summary>
    /// <returns>Горизонталь центра бегунка</returns>
    private double TargetX() =>
        THUMB_RADIUS + (Math.Clamp(Value, MIN_VALUE, MAX_VALUE) - MIN_VALUE) / (MAX_VALUE - MIN_VALUE) * Math.Max(1, ActualWidth - THUMB_RADIUS * 2);

    /// <summary>
    /// Включает покадровый расчёт пружин
    /// </summary>
    private void StartLoop()
    {
        if (_looping || !IsVisible)
            return;

        _looping = true;
        _lastStep = DateTime.UtcNow;
        CompositionTarget.Rendering += OnFrame;
    }

    /// <summary>
    /// Считает шаг пружин и перерисовывает полосу. Когда всё остановилось, расчёт выключается
    /// </summary>
    /// <param name="sender">Источник события</param>
    /// <param name="e">Аргументы события</param>
    private void OnFrame(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var left = Math.Min((now - _lastStep).TotalSeconds, 0.1);
        _lastStep = now;
        var target = TargetX();

        while (left > 0)
        {
            var step = Math.Min(left, MAX_STEP);
            _thumbSpeed += (THUMB_STIFFNESS * (target - _thumb) - THUMB_DAMPING * _thumbSpeed) * step;
            _thumb += _thumbSpeed * step;
            _tailSpeed += (TAIL_STIFFNESS * (_thumb - _tail) - TAIL_DAMPING * _tailSpeed) * step;
            _tail += _tailSpeed * step;
            left -= step;
        }

        InvalidateVisual();
        if (Math.Abs(target - _thumb) < 0.05 && Math.Abs(_thumbSpeed) < 0.5 && Math.Abs(_thumb - _tail) < 0.05 && Math.Abs(_tailSpeed) < 0.5)
        {
            _thumb = _tail = target;
            _thumbSpeed = _tailSpeed = 0;
            _looping = false;
            CompositionTarget.Rendering -= OnFrame;
        }
    }

    #region Input

    /// <summary>
    /// Ставит бегунок в точку нажатия
    /// </summary>
    /// <param name="e">Аргументы нажатия</param>
    protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        CaptureMouse();
        MoveTo(e.GetPosition(this).X);
        e.Handled = true;
    }

    /// <summary>
    /// Тянет бегунок за курсором
    /// </summary>
    /// <param name="e">Аргументы движения</param>
    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        if (!IsMouseCaptured)
            return;

        MoveTo(e.GetPosition(this).X);
        e.Handled = true;
    }

    /// <summary>
    /// Отпускает бегунок
    /// </summary>
    /// <param name="e">Аргументы отпускания</param>
    protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!IsMouseCaptured)
            return;

        ReleaseMouseCapture();
        e.Handled = true;
    }

    /// <summary>
    /// Переводит координату курсора в целое число минут
    /// </summary>
    /// <param name="x">Горизонталь курсора внутри полосы</param>
    private void MoveTo(double x)
    {
        var span = Math.Max(1, ActualWidth - THUMB_RADIUS * 2);
        var ratio = Math.Clamp((x - THUMB_RADIUS) / span, 0, 1);
        var minutes = MIN_VALUE + ratio * (MAX_VALUE - MIN_VALUE);

        Value = Math.Clamp(Math.Round(minutes), MIN_VALUE, MAX_VALUE);
    }

    #endregion Input

    #region Drawing

    /// <summary>
    /// Рисует дорожку, залитую часть и «жидкий» бегунок: круг, хвост-каплю и шейку между ними
    /// </summary>
    /// <param name="drawingContext">Контекст кадра</param>
    protected override void OnRender(DrawingContext drawingContext)
    {
        var accent = (SolidColorBrush)FindResource("AccentBrush");
        var line = (System.Windows.Media.Brush)FindResource("LineBrush");
        var span = Math.Max(1, ActualWidth - THUMB_RADIUS * 2);
        var target = TargetX();
        if (double.IsNaN(_thumb))
            _thumb = _tail = target;

        var centerY = ActualHeight / 2;
        var track = new Rect(THUMB_RADIUS, centerY - TRACK_HEIGHT / 2, span, TRACK_HEIGHT);
        var thumb = new System.Windows.Point(_thumb, centerY);
        var stretch = Math.Clamp(Math.Abs(_thumb - _tail) / 40, 0, 1);
        var tailRadius = THUMB_RADIUS * (0.95 - 0.35 * stretch);

        drawingContext.DrawRoundedRectangle(line, null, track, 3, 3);
        if (_thumb > track.X)
            drawingContext.DrawRoundedRectangle(accent, null, new Rect(track.X, track.Y, _thumb - track.X, TRACK_HEIGHT), 3, 3);

        drawingContext.DrawEllipse(Glow(accent), null, thumb, GLOW_RADIUS, GLOW_RADIUS);

        var drop = new GeometryGroup { FillRule = FillRule.Nonzero };
        drop.Children.Add(new EllipseGeometry(thumb, THUMB_RADIUS, THUMB_RADIUS));
        drop.Children.Add(new EllipseGeometry(new System.Windows.Point(_tail, centerY), tailRadius, tailRadius));
        if (Metaball.Bridge(thumb, THUMB_RADIUS, new System.Windows.Point(_tail, centerY), tailRadius) is { } neck)
            drop.Children.Add(neck);

        drawingContext.DrawGeometry(Lighter(accent), null, drop);
        drawingContext.DrawEllipse(null, new System.Windows.Media.Pen((System.Windows.Media.Brush)FindResource("TextBrush"), 2), thumb, THUMB_RADIUS - 1, THUMB_RADIUS - 1);
    }

    /// <summary>
    /// Чуть светлее основного цвета: капля заметна на залитой части дорожки
    /// </summary>
    /// <param name="accent">Основной цвет панели</param>
    /// <returns>Замороженная кисть светлее акцента</returns>
    private static System.Windows.Media.Brush Lighter(SolidColorBrush accent)
    {
        var color = accent.Color;
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)(color.R + (255 - color.R) * 0.28), (byte)(color.G + (255 - color.G) * 0.28), (byte)(color.B + (255 - color.B) * 0.28)));

        brush.Freeze();

        return brush;
    }

    /// <summary>
    /// Мягкое акцентное пятно под бегунком
    /// </summary>
    /// <param name="accent">Основной цвет панели</param>
    /// <returns>Кисть, которую можно рисовать много раз</returns>
    private static System.Windows.Media.Brush Glow(SolidColorBrush accent)
    {
        var color = accent.Color;
        var glow = new RadialGradientBrush(color, System.Windows.Media.Color.FromArgb(0, color.R, color.G, color.B));

        glow.Freeze();

        return glow;
    }

    #endregion Drawing
}
