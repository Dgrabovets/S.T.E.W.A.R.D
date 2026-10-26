using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Steward.Models;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Cursors = System.Windows.Input.Cursors;

namespace Steward.Controls;

/// <summary>
/// Меню из капель: большая капля цвета состояния и круглые кнопки, которые выезжают из неё веером и слипаются с ней, пока двигаются
/// Рисуется кодом, значки и подписи кладутся поверх капель отдельно, поэтому остаются чёткими
/// </summary>
public sealed class GooeyCluster : FrameworkElement
{
    /// <summary>
    /// Радиус главной капли
    /// </summary>
    private const double MAIN_RADIUS = 27;

    /// <summary>
    /// Радиус кнопки
    /// </summary>
    private const double ITEM_RADIUS = 22;

    /// <summary>
    /// Расстояние от главной капли до первой кнопки
    /// </summary>
    private const double FIRST_GAP = 66;

    /// <summary>
    /// Расстояние между центрами соседних кнопок
    /// </summary>
    private const double SPACING = 60;

    /// <summary>
    /// Дополнительный промежуток перед кнопкой, у которой есть разделитель
    /// </summary>
    private const double GROUP_GAP = 16;

    /// <summary>
    /// Отступ центра главной капли от правого края
    /// </summary>
    private const double EDGE = 38;

    /// <summary>
    /// Высота полосы подписи над кнопками
    /// </summary>
    private const double LABEL_BAND = 30;

    /// <summary>
    /// Жёсткость пружины: чем больше, тем быстрее кнопка выезжает
    /// </summary>
    private const double STIFFNESS = 260;

    /// <summary>
    /// Затухание пружины: чем меньше, тем сильнее отскок
    /// </summary>
    private const double DAMPING = 17;

    /// <summary>
    /// Пауза между выездом соседних кнопок, секунд
    /// </summary>
    private const double STAGGER = 0.045;

    /// <summary>
    /// Самый крупный шаг расчёта пружины, секунд: большие шаги разгоняют пружину
    /// </summary>
    private const double MAX_STEP = 1.0 / 120;

    /// <summary>
    /// С какого раскрытия кнопка считается стоящей на месте: от него и выше перемычка с главной каплей не рисуется
    /// </summary>
    private const double SETTLED = 0.97;

    /// <summary>
    /// Значение «ничего не выбрано»
    /// </summary>
    private const int NONE = -2;

    /// <summary>
    /// Значение «главная капля»
    /// </summary>
    private const int MAIN = -1;

    /// <summary>
    /// Кнопки меню
    /// </summary>
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
        nameof(Items),
        typeof(IReadOnlyList<GooeyItem>),
        typeof(GooeyCluster),
        new FrameworkPropertyMetadata(Array.Empty<GooeyItem>(), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, (cluster, _) => ((GooeyCluster)cluster).Reset()));

    /// <summary>
    /// Раскрыто ли меню
    /// </summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen),
        typeof(bool),
        typeof(GooeyCluster),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, (cluster, _) => ((GooeyCluster)cluster).OnOpenChanged()));

    /// <summary>
    /// Заливка капель
    /// </summary>
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(GooeyCluster), new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Тонкий контур вокруг общего силуэта
    /// </summary>
    public static readonly DependencyProperty OutlineProperty = DependencyProperty.Register(
        nameof(Outline), typeof(Brush), typeof(GooeyCluster), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Цвет значков и подписей
    /// </summary>
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(GooeyCluster), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Цвет состояния: им светится главная капля и обводится выбранная кнопка
    /// </summary>
    public static readonly DependencyProperty StateBrushProperty = DependencyProperty.Register(
        nameof(StateBrush), typeof(Brush), typeof(GooeyCluster), new FrameworkPropertyMetadata(Brushes.Green, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Шрифт значков
    /// </summary>
    private static readonly Typeface IconFace = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    /// <summary>
    /// Шрифт подписей
    /// </summary>
    private static readonly Typeface LabelFace = new("Segoe UI");

    /// <summary>
    /// Насколько раскрыта каждая кнопка: 0 — спрятана в главной капле, 1 — на своём месте. Пружина даёт отскок за единицу
    /// </summary>
    private double[] _open = [];

    /// <summary>
    /// Скорость раскрытия каждой кнопки
    /// </summary>
    private double[] _velocity = [];

    /// <summary>
    /// Когда меню открыли или закрыли в последний раз
    /// </summary>
    private DateTime _toggledAt = DateTime.UtcNow;

    /// <summary>
    /// Когда считали пружину в последний раз
    /// </summary>
    private DateTime _lastStep = DateTime.UtcNow;

    /// <summary>
    /// Идёт ли покадровый расчёт
    /// </summary>
    private bool _looping;

    /// <summary>
    /// Кнопка под курсором
    /// </summary>
    private int _hover = NONE;

    /// <summary>
    /// Кнопка, на которой стоит клавиатурный выбор
    /// </summary>
    private int _focusIndex = NONE;

    /// <summary>
    /// Создаёт меню и подписывается на показ и скрытие, чтобы не считать кадры невидимому окну
    /// </summary>
    public GooeyCluster()
    {
        Focusable = true;
        FocusVisualStyle = null;
        Cursor = Cursors.Hand;
        Loaded += (_, _) => StartLoop();
        Unloaded += (_, _) => StopLoop();
        IsVisibleChanged += (_, _) => StartLoop();
    }

    /// <summary>
    /// Кнопки меню
    /// </summary>
    public IReadOnlyList<GooeyItem> Items
    {
        get => (IReadOnlyList<GooeyItem>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>
    /// Раскрыто ли меню
    /// </summary>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>
    /// Заливка капель
    /// </summary>
    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>
    /// Контур силуэта
    /// </summary>
    public Brush Outline
    {
        get => (Brush)GetValue(OutlineProperty);
        set => SetValue(OutlineProperty, value);
    }

    /// <summary>
    /// Цвет значков и подписей
    /// </summary>
    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>
    /// Цвет состояния
    /// </summary>
    public Brush StateBrush
    {
        get => (Brush)GetValue(StateBrushProperty);
        set => SetValue(StateBrushProperty, value);
    }

    /// <summary>
    /// Сразу ставит кнопки на места без анимации: для снимков и для тех, кто отключил анимацию
    /// </summary>
    public void SnapToState()
    {
        for (var index = 0; index < _open.Length; index++)
        {
            _open[index] = IsOpen ? 1 : 0;
            _velocity[index] = 0;
        }

        InvalidateVisual();
    }

    /// <summary>
    /// Нужный размер: по ширине всё, что вместит ряд кнопок, по высоте капля и полоса подписи
    /// </summary>
    /// <param name="availableSize">Доступное место</param>
    /// <returns>Размер меню</returns>
    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? EDGE + FIRST_GAP + SPACING * Math.Max(0, Items.Count - 1) + GROUP_GAP * Items.Count(item => item.DividerBefore) + ITEM_RADIUS + 8 : availableSize.Width, LABEL_BAND + MAIN_RADIUS * 2 + 8);

    /// <summary>
    /// Рисует капли, перемычки, значки и подпись
    /// </summary>
    /// <param name="drawingContext">Контекст рисования</param>
    protected override void OnRender(DrawingContext drawingContext)
    {
        var main = MainCenter();
        var circles = new List<(Point Center, double Radius)> { (main, MAIN_RADIUS) };
        var shapes = new GeometryGroup { FillRule = FillRule.Nonzero };
        shapes.Children.Add(new EllipseGeometry(main, MAIN_RADIUS, MAIN_RADIUS));

        for (var index = 0; index < Items.Count; index++)
        {
            var reach = _open[index];
            if (reach < 0.02)
                continue;

            var center = Lerp(main, ItemCenter(index), reach);
            var radius = ITEM_RADIUS * (_hover == index || _focusIndex == index ? 1.12 : 1) * Math.Min(1, 0.55 + reach);
            shapes.Children.Add(new EllipseGeometry(center, radius, radius));
            circles.Add((center, radius));

            if (reach < SETTLED)
                AddBridge(shapes, main, MAIN_RADIUS, center, radius);
        }

        drawingContext.DrawGeometry(null, new Pen(Outline, 2.4), shapes);
        drawingContext.DrawGeometry(Fill, null, shapes);

        DrawDividers(drawingContext);

        DrawMain(drawingContext, main);
        DrawItems(drawingContext, circles);
        DrawLabel(drawingContext);
    }

    /// <summary>
    /// Двигает мышь: подсвечивает кнопку под курсором
    /// </summary>
    /// <param name="e">Аргументы мыши</param>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        var hit = HitTest(e.GetPosition(this));
        if (hit == _hover)
            return;

        _hover = hit;
        Cursor = hit == NONE ? Cursors.Arrow : Cursors.Hand;
        StartLoop();
        InvalidateVisual();
    }

    /// <summary>
    /// Мышь ушла с меню: подсветка снимается
    /// </summary>
    /// <param name="e">Аргументы мыши</param>
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _hover = NONE;
        InvalidateVisual();
    }

    /// <summary>
    /// Нажатие на главную каплю открывает и закрывает меню, на кнопку выполняет её действие
    /// </summary>
    /// <param name="e">Аргументы мыши</param>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        var hit = HitTest(e.GetPosition(this));
        if (hit == MAIN)
            IsOpen = !IsOpen;
        else if (hit >= 0)
            Items[hit].Activate();

        e.Handled = true;
    }

    /// <summary>
    /// Стрелки и Tab двигают выбор по кнопкам, Enter и пробел выполняют действие
    /// </summary>
    /// <param name="e">Аргументы клавиши</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Items.Count == 0)
            return;

        switch (e.Key)
        {
            case Key.Left:
                _focusIndex = _focusIndex == NONE ? 0 : Math.Min(Items.Count - 1, _focusIndex + 1);
                break;
            case Key.Right:
                _focusIndex = _focusIndex == NONE ? 0 : Math.Max(0, _focusIndex - 1);
                break;
            case Key.Enter or Key.Space when _focusIndex >= 0:
                Items[_focusIndex].Activate();
                break;
            default:
                return;
        }

        e.Handled = true;
        InvalidateVisual();
    }

    /// <summary>
    /// Сбрасывает раскрытие, когда сменился набор кнопок
    /// </summary>
    private void Reset()
    {
        _open = new double[Items.Count];
        _velocity = new double[Items.Count];
        _focusIndex = NONE;
        _toggledAt = DateTime.UtcNow;
        StartLoop();
    }

    /// <summary>
    /// Запоминает момент, когда меню открыли или закрыли, и запускает расчёт пружины
    /// </summary>
    private void OnOpenChanged()
    {
        _toggledAt = DateTime.UtcNow;
        _lastStep = DateTime.UtcNow;
        StartLoop();
    }

    /// <summary>
    /// Включает покадровый расчёт
    /// </summary>
    private void StartLoop()
    {
        if (!IsLoaded || !IsVisible || _looping)
            return;

        CompositionTarget.Rendering += OnFrame;
        _looping = true;
        _lastStep = DateTime.UtcNow;
    }

    /// <summary>
    /// Выключает покадровый расчёт
    /// </summary>
    private void StopLoop()
    {
        if (!_looping)
            return;

        CompositionTarget.Rendering -= OnFrame;
        _looping = false;
    }

    /// <summary>
    /// Считает шаг пружины, перерисовывает кадр и выключает расчёт, когда всё остановилось
    /// </summary>
    /// <param name="sender">Источник события</param>
    /// <param name="e">Аргументы события</param>
    private void OnFrame(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var remaining = Math.Min((now - _lastStep).TotalSeconds, 0.1);
        _lastStep = now;
        var elapsed = (now - _toggledAt).TotalSeconds;
        var moving = false;

        for (var index = 0; index < _open.Length; index++)
        {
            var target = IsOpen && elapsed >= STAGGER * index ? 1.0 : 0.0;
            var left = remaining;
            while (left > 0)
            {
                var step = Math.Min(left, MAX_STEP);
                _velocity[index] += (STIFFNESS * (target - _open[index]) - DAMPING * _velocity[index]) * step;
                _open[index] += _velocity[index] * step;
                left -= step;
            }

            if (Math.Abs(target - _open[index]) > 0.001 || Math.Abs(_velocity[index]) > 0.001 || (IsOpen && target == 0))
                moving = true;
            else
                _open[index] = target;
        }

        InvalidateVisual();
        if (!moving)
            StopLoop();
    }

    /// <summary>
    /// Рисует главную каплю: кольцо и точку цвета состояния
    /// </summary>
    /// <param name="context">Контекст рисования</param>
    /// <param name="main">Центр главной капли</param>
    private void DrawMain(DrawingContext context, Point main)
    {
        var grow = _hover == MAIN ? 1.08 : 1;
        context.DrawEllipse(null, new Pen(StateBrush, 1.5), main, 14 * grow, 14 * grow);
        context.DrawEllipse(StateBrush, null, main, 8 * grow, 8 * grow);
    }

    /// <summary>
    /// Рисует вертикальную черту перед кнопками, которые начинают новую группу
    /// </summary>
    /// <param name="context">Контекст рисования</param>
    private void DrawDividers(DrawingContext context)
    {
        var pen = new Pen(Outline, 1);
        for (var index = 1; index < Items.Count; index++)
        {
            if (!Items[index].DividerBefore || _open[index] < 0.9 || _open[index - 1] < 0.9)
                continue;

            var middle = (ItemCenter(index).X + ItemCenter(index - 1).X) / 2;
            var y = MainCenter().Y;
            context.DrawLine(pen, new Point(middle, y - ITEM_RADIUS), new Point(middle, y + ITEM_RADIUS));
        }
    }

    /// <summary>
    /// Рисует значки кнопок и обводку клавиатурного выбора
    /// </summary>
    /// <param name="context">Контекст рисования</param>
    /// <param name="circles">Положение и радиус нарисованных капель, первая из них главная</param>
    private void DrawItems(DrawingContext context, List<(Point Center, double Radius)> circles)
    {
        var shown = 1;
        for (var index = 0; index < Items.Count; index++)
        {
            if (_open[index] < 0.02)
                continue;

            var (center, radius) = circles[shown++];
            var glyph = new FormattedText(Items[index].Glyph, CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight, IconFace, 19 * (radius / ITEM_RADIUS), Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            context.DrawText(glyph, new Point(center.X - glyph.Width / 2, center.Y - glyph.Height / 2));

            if (IsKeyboardFocused && _focusIndex == index)
                context.DrawEllipse(null, new Pen(Foreground, 2), center, radius + 4, radius + 4);
        }
    }

    /// <summary>
    /// Пишет подпись над кнопкой, на которую наведена мышь или выбор
    /// </summary>
    /// <param name="context">Контекст рисования</param>
    private void DrawLabel(DrawingContext context)
    {
        var index = _hover >= 0 ? _hover : IsKeyboardFocused ? _focusIndex : NONE;
        if (index < 0 || index >= Items.Count || _open[index] < 0.9)
            return;

        var text = new FormattedText(Items[index].Label, CultureInfo.CurrentUICulture, System.Windows.FlowDirection.LeftToRight, LabelFace, 14, Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var x = Math.Clamp(ItemCenter(index).X - text.Width / 2, 4, Math.Max(4, ActualWidth - text.Width - 4));
        context.DrawText(text, new Point(x, 4));
    }

    /// <summary>
    /// Определяет, что лежит под точкой
    /// </summary>
    /// <param name="point">Точка в координатах меню</param>
    /// <returns>Номер кнопки, <see cref="MAIN"/> для главной капли или <see cref="NONE"/></returns>
    private int HitTest(Point point)
    {
        for (var index = Items.Count - 1; index >= 0; index--)
        {
            if (_open[index] > 0.6 && (point - Lerp(MainCenter(), ItemCenter(index), _open[index])).Length <= ITEM_RADIUS + 2)
                return index;
        }

        return (point - MainCenter()).Length <= MAIN_RADIUS ? MAIN : NONE;
    }

    /// <summary>
    /// Добавляет перемычку между двумя каплями, если они достаточно близко
    /// </summary>
    /// <param name="shapes">Общий силуэт</param>
    /// <param name="first">Центр первой капли</param>
    /// <param name="firstRadius">Радиус первой капли</param>
    /// <param name="second">Центр второй капли</param>
    /// <param name="secondRadius">Радиус второй капли</param>
    private static void AddBridge(GeometryGroup shapes, Point first, double firstRadius, Point second, double secondRadius)
    {
        if (Metaball.Bridge(first, firstRadius, second, secondRadius) is { } bridge)
            shapes.Children.Add(bridge);
    }

    /// <summary>
    /// Центр главной капли
    /// </summary>
    /// <returns>Точка у правого нижнего края</returns>
    private Point MainCenter() => new(ActualWidth - EDGE, ActualHeight - MAIN_RADIUS - 4);

    /// <summary>
    /// Место кнопки, когда она раскрыта
    /// </summary>
    /// <param name="index">Номер кнопки</param>
    /// <returns>Центр кнопки</returns>
    private Point ItemCenter(int index) => new(MainCenter().X - FIRST_GAP - SPACING * index - GROUP_GAP * Items.Take(index + 1).Count(item => item.DividerBefore), MainCenter().Y);

    /// <summary>
    /// Точка между двумя на нужной доле пути, доля может выходить за единицу, это даёт отскок
    /// </summary>
    /// <param name="from">Начало</param>
    /// <param name="to">Конец</param>
    /// <param name="share">Доля пути</param>
    /// <returns>Точка на прямой между ними</returns>
    private static Point Lerp(Point from, Point to, double share) => new(from.X + (to.X - from.X) * share, from.Y + (to.Y - from.Y) * share);
}