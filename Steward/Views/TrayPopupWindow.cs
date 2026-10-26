using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using System.Windows.Threading;

namespace Steward.Views;

/// <summary>
/// Основа окон, которые открываются у иконки в трее: сразу появляются у курсора, прячутся по Escape и когда пропал фокус. Повторный щелчок по иконке закрывает окно, а не открывает заново
/// </summary>
public abstract class TrayPopupWindow : Window
{
    /// <summary>
    /// Отступ окна от края рабочей области, в аппаратно-независимых пикселях
    /// </summary>
    private const double SCREEN_MARGIN = 8;

    /// <summary>
    /// Сдвиг окна влево от курсора, чтобы оно открывалось над иконкой, а не поверх неё
    /// </summary>
    private const double CURSOR_OFFSET_X = 28;

    /// <summary>
    /// Сдвиг окна вверх от курсора
    /// </summary>
    private const double CURSOR_OFFSET_Y = 8;

    /// <summary>
    /// Сколько миллисекунд до нажатия на иконку окно могло закрыться по потере фокуса и всё равно считаться открытым в момент нажатия. Нажатие на панель задач отбирает фокус раньше, чем приходит событие иконки
    /// </summary>
    private const long PRESS_GRACE_MS = 400;

    /// <summary>
    /// На сколько пикселей за экран уходит окно при прогреве
    /// </summary>
    private const double WARM_OFFSET = 20000;

    /// <summary>
    /// Щелчок по иконке ещё не закончился, повторно открывать и закрывать окно нельзя
    /// </summary>
    private bool _ignoreToggle;

    /// <summary>
    /// Нажатие на иконку уже закрыло окно, и следующий щелчок по ней открывать его заново не должен
    /// </summary>
    private bool _swallowClick;

    /// <summary>
    /// Момент последнего закрытия по потере фокуса, в миллисекундах от старта системы. Ноль, пока такого закрытия не было
    /// </summary>
    private long _dismissedAt;

    /// <summary>
    /// Нижний край окна, который держится на месте, если окно над иконкой меняет высоту. Пусто, если окно стоит под курсором
    /// </summary>
    private double? _anchorBottom;

    /// <summary>
    /// Создаёт окно и подписывается на потерю фокуса и смену размера
    /// </summary>
    protected TrayPopupWindow()
    {
        Deactivated += OnDeactivated;
        SizeChanged += OnSizeChanged;
    }

    /// <summary>
    /// Окно спрятано пользователем: по Escape или потому, что фокус ушёл
    /// </summary>
    public event EventHandler? Dismissed;

    /// <summary>
    /// Сообщает окну, что кнопку мыши нажали на иконке. Если окно в этот момент открыто или только что закрылось по потере фокуса, этот щелчок должен его закрыть, а не открыть заново
    /// </summary>
    public void NotifyIconPressed()
    {
        var wasOpen = IsVisible;
        _swallowClick = wasOpen || (_dismissedAt != 0 && Environment.TickCount64 - _dismissedAt < PRESS_GRACE_MS);
        if (!wasOpen)
            return;

        Hide();
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Открывает окно по щелчку на иконку или прячет его, если оно уже видно
    /// </summary>
    public void ToggleFromIcon()
    {
        if (_swallowClick)
        {
            _swallowClick = false;

            return;
        }

        if (_ignoreToggle)
            return;

        if (IsVisible)
        {
            Hide();

            return;
        }

        Open();
    }

    /// <summary>
    /// Прогревает окно: один раз показывает его далеко за экраном и прячет. Тогда первое настоящее открытие не тратит четверть секунды на разбор разметки и создание окна
    /// </summary>
    public void Warm()
    {
        if (IsVisible)
            return;

        _ignoreToggle = true;
        var activated = ShowActivated;
        ShowActivated = false;
        Left = -WARM_OFFSET;
        Top = -WARM_OFFSET;
        Show();
        UpdateLayout();
        Hide();
        ShowActivated = activated;
        _ignoreToggle = false;
    }

    /// <summary>
    /// Вызывается, когда окно показано и стоит на месте
    /// </summary>
    protected virtual void OnOpened()
    {
    }

    /// <summary>
    /// Прячет окно по Escape
    /// </summary>
    /// <param name="e">Аргументы клавиши</param>
    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            Dismissed?.Invoke(this, EventArgs.Empty);
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>
    /// Показывает окно сразу в нужной точке: размер известен заранее из раскладки, поэтому первый кадр не мелькает в углу экрана
    /// </summary>
    private void Open()
    {
        _ignoreToggle = true;
        var cursor = System.Windows.Forms.Cursor.Position;
        var screen = System.Windows.Forms.Screen.FromPoint(cursor);

        // Дескриптор нужен, чтобы знать масштаб экрана до показа окна
        _ = new WindowInteropHelper(this).EnsureHandle();
        var size = MeasureContent();
        PlaceNearCursor(cursor, screen, size);
        Show();
        Activate();
        OnOpened();
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, ReleaseToggle);
    }

    /// <summary>
    /// Считает, какого размера окно будет, не показывая его
    /// </summary>
    /// <returns>Ширина окна и высота, которой хватит содержимому</returns>
    private Size MeasureContent()
    {
        if (Content is not FrameworkElement root)
            return new Size(Width, Height);

        root.Measure(new Size(Width, double.PositiveInfinity));

        return new Size(Width, Math.Max(MinHeight, root.DesiredSize.Height));
    }

    /// <summary>
    /// Прячет окно, когда фокус ушёл. Щелчок, который его открыл, сюда не попадает
    /// </summary>
    /// <param name="sender">Окно</param>
    /// <param name="e">Аргументы события</param>
    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_ignoreToggle || !IsVisible)
            return;

        _dismissedAt = Environment.TickCount64;
        Hide();
        Dismissed?.Invoke(this, EventArgs.Empty);
        _ignoreToggle = true;
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, ReleaseToggle);
    }

    /// <summary>
    /// Держит нижний край окна на месте, когда содержимое меняет высоту: окно растёт и сжимается вверх от иконки
    /// </summary>
    /// <param name="sender">Окно</param>
    /// <param name="e">Старый и новый размер</param>
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsVisible && _anchorBottom is { } bottom && e.HeightChanged)
            Top = bottom - e.NewSize.Height;
    }

    /// <summary>
    /// Снова разрешает открытие и закрытие, когда щелчок по иконке уже обработан
    /// </summary>
    private void ReleaseToggle() => _ignoreToggle = false;

    /// <summary>
    /// Ставит окно над курсором и не выпускает его за край экрана
    /// </summary>
    /// <param name="cursor">Точка курсора в физических пикселях</param>
    /// <param name="screen">Экран, на котором лежит курсор</param>
    /// <param name="size">Размер, какой окно займёт</param>
    private void PlaceNearCursor(System.Drawing.Point cursor, System.Windows.Forms.Screen screen, Size size)
    {
        var area = screen.WorkingArea;
        var toDip = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var origin = toDip.Transform(new Point(cursor.X, cursor.Y));
        var topLeft = toDip.Transform(new Point(area.Left, area.Top));
        var bottomRight = toDip.Transform(new Point(area.Right, area.Bottom));
        var x = Math.Clamp(origin.X - size.Width + CURSOR_OFFSET_X, topLeft.X + SCREEN_MARGIN, Math.Max(topLeft.X + SCREEN_MARGIN, bottomRight.X - size.Width - SCREEN_MARGIN));
        var y = origin.Y - size.Height - CURSOR_OFFSET_Y;
        _anchorBottom = y + size.Height;
        if (y < topLeft.Y + SCREEN_MARGIN)
        {
            y = Math.Min(origin.Y + CURSOR_OFFSET_Y, Math.Max(topLeft.Y + SCREEN_MARGIN, bottomRight.Y - size.Height - SCREEN_MARGIN));
            _anchorBottom = null;
        }

        Left = x;
        Top = y;
    }
}
