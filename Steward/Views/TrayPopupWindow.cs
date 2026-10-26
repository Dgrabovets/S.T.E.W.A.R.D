using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Point = System.Windows.Point;
using System.Windows.Threading;

namespace Steward.Views;

/// <summary>
/// Основа окон, которые открываются у иконки в трее: появляются у курсора, прячутся по Escape и когда пропал фокус
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
    /// Сколько миллисекунд после закрытия по потере фокуса щелчок по иконке считается тем самым щелчком, который окно закрыл. Нажатие на иконку отбирает фокус раньше, чем приходит сам щелчок, и без этого окно тут же открывалось бы снова
    /// </summary>
    private const long DISMISS_GRACE_MS = 450;

    /// <summary>
    /// Щелчок по иконке ещё не закончился, повторно открывать и закрывать окно нельзя
    /// </summary>
    private bool _ignoreToggle;

    /// <summary>
    /// Момент последнего закрытия по потере фокуса
    /// </summary>
    private long _dismissedAt = long.MinValue;

    /// <summary>
    /// Создаёт окно и подписывается на потерю фокуса
    /// </summary>
    protected TrayPopupWindow()
    {
        Deactivated += OnDeactivated;
    }

    /// <summary>
    /// Окно спрятано пользователем: по Escape или потому, что фокус ушёл
    /// </summary>
    public event EventHandler? Dismissed;

    /// <summary>
    /// Открывает окно по щелчку на иконку или прячет его, если оно уже видно
    /// </summary>
    public void ToggleFromIcon()
    {
        if (_ignoreToggle)
            return;

        if (IsVisible)
        {
            Hide();

            return;
        }

        if (Environment.TickCount64 - _dismissedAt < DISMISS_GRACE_MS)
        {
            _dismissedAt = long.MinValue;

            return;
        }

        Open();
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
    /// Показывает окно сразу в нужной точке, без кадра в чужом месте и без ухода в прозрачность
    /// </summary>
    private void Open()
    {
        _ignoreToggle = true;
        var cursor = System.Windows.Forms.Cursor.Position;
        var screen = System.Windows.Forms.Screen.FromPoint(cursor);

        BeginAnimation(OpacityProperty, null);
        Opacity = 0;
        Left = screen.WorkingArea.Left;
        Top = screen.WorkingArea.Top;
        Show();
        UpdateLayout();
        PlaceNearCursor(cursor, screen);
        Opacity = 1;
        Activate();
        OnOpened();
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, ReleaseToggle);
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
    /// Снова разрешает открытие и закрытие, когда щелчок по иконке уже обработан
    /// </summary>
    private void ReleaseToggle() => _ignoreToggle = false;

    /// <summary>
    /// Ставит окно над курсором и не выпускает его за край экрана
    /// </summary>
    /// <param name="cursor">Точка курсора в физических пикселях</param>
    /// <param name="screen">Экран, на котором лежит курсор</param>
    private void PlaceNearCursor(System.Drawing.Point cursor, System.Windows.Forms.Screen screen)
    {
        var area = screen.WorkingArea;
        var toDip = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var origin = toDip.Transform(new Point(cursor.X, cursor.Y));
        var topLeft = toDip.Transform(new Point(area.Left, area.Top));
        var bottomRight = toDip.Transform(new Point(area.Right, area.Bottom));
        var x = Math.Clamp(origin.X - ActualWidth + CURSOR_OFFSET_X, topLeft.X + SCREEN_MARGIN, Math.Max(topLeft.X + SCREEN_MARGIN, bottomRight.X - ActualWidth - SCREEN_MARGIN));
        var y = origin.Y - ActualHeight - CURSOR_OFFSET_Y;
        if (y < topLeft.Y + SCREEN_MARGIN)
            y = Math.Min(origin.Y + CURSOR_OFFSET_Y, Math.Max(topLeft.Y + SCREEN_MARGIN, bottomRight.Y - ActualHeight - SCREEN_MARGIN));

        Left = x;
        Top = y;
    }
}