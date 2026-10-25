using System.Windows;
using System.Windows.Media;

namespace Steward.Controls;

/// <summary>
/// Горизонтальный ползунок интервала в цветах панели
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
        ((IntervalBar)element).InvalidateVisual();
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
    /// Рисует дорожку слева направо и круглый бегунок
    /// </summary>
    /// <param name="drawingContext">Контекст кадра</param>
    protected override void OnRender(DrawingContext drawingContext)
    {
        var accent = (SolidColorBrush)FindResource("AccentBrush");
        var line = (System.Windows.Media.Brush)FindResource("LineBrush");
        var span = Math.Max(1, ActualWidth - THUMB_RADIUS * 2);
        var ratio = (Math.Clamp(Value, MIN_VALUE, MAX_VALUE) - MIN_VALUE) / (MAX_VALUE - MIN_VALUE);
        var thumbX = THUMB_RADIUS + ratio * span;
        var centerY = ActualHeight / 2;
        var track = new Rect(THUMB_RADIUS, centerY - TRACK_HEIGHT / 2, span, TRACK_HEIGHT);

        drawingContext.DrawRoundedRectangle(line, null, track, 3, 3);
        if (thumbX > track.X)
        {
            drawingContext.DrawRoundedRectangle(
                accent,
                null,
                new Rect(track.X, track.Y, thumbX - track.X, TRACK_HEIGHT),
                3,
                3);
        }

        drawingContext.DrawEllipse(Glow(accent), null, new System.Windows.Point(thumbX, centerY), GLOW_RADIUS, GLOW_RADIUS);
        drawingContext.DrawEllipse(accent, new System.Windows.Media.Pen((System.Windows.Media.Brush)FindResource("TextBrush"), 2), new System.Windows.Point(thumbX, centerY), THUMB_RADIUS, THUMB_RADIUS);
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
