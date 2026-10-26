using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Point = System.Windows.Point;
using System.Windows.Threading;

namespace Steward.Views;

/// <summary>
/// Основа окон, которые открываются у иконки в трее: выезжают из точки щелчка, прячутся по Escape и когда пропал фокус. Повторный щелчок по иконке закрывает окно, а не открывает заново
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
    /// Во сколько раз окно меньше в начале появления
    /// </summary>
    private const double ENTER_SCALE = 0.7;

    /// <summary>
    /// Во сколько раз окно меньше в конце исчезновения
    /// </summary>
    private const double EXIT_SCALE = 0.86;

    /// <summary>
    /// Щелчок по иконке ещё не закончился, повторно открывать и закрывать окно нельзя
    /// </summary>
    private bool _ignoreToggle;

    /// <summary>
    /// Окно сейчас уходит: оно ещё видно, но считается закрытым
    /// </summary>
    private bool _closing;

    /// <summary>
    /// Момент последнего закрытия по потере фокуса
    /// </summary>
    private long _dismissedAt = long.MinValue;

    /// <summary>
    /// Сжатие окна при появлении и исчезновении
    /// </summary>
    private readonly ScaleTransform _scale = new(1, 1);

    /// <summary>
    /// Сдвиг окна при появлении и исчезновении
    /// </summary>
    private readonly TranslateTransform _shift = new();

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
    /// Открывает окно по щелчку на иконку или закрывает его, если оно уже видно
    /// </summary>
    public void ToggleFromIcon()
    {
        if (_ignoreToggle || _closing)
            return;

        if (IsVisible)
        {
            Dismiss();

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
    /// Плавно убирает окно: оно чуть сжимается, уходит к иконке и гаснет, потом прячется
    /// </summary>
    public void Dismiss()
    {
        if (!IsVisible || _closing)
            return;

        _closing = true;
        var duration = TimeSpan.FromMilliseconds(150);
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var fade = new DoubleAnimation(0, duration) { EasingFunction = ease };
        fade.Completed += (_, _) =>
        {
            Hide();
            ResetMotion();
            _closing = false;
        };

        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(EXIT_SCALE, duration) { EasingFunction = ease });
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(EXIT_SCALE, duration) { EasingFunction = ease });
        _shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, duration) { EasingFunction = ease });
        BeginAnimation(OpacityProperty, fade);
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
            Dismiss();
            Dismissed?.Invoke(this, EventArgs.Empty);
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>
    /// Показывает окно сразу в нужной точке, без кадра в чужом месте, и выпускает его из точки щелчка
    /// </summary>
    private void Open()
    {
        _ignoreToggle = true;
        var cursor = System.Windows.Forms.Cursor.Position;
        var screen = System.Windows.Forms.Screen.FromPoint(cursor);

        ResetMotion();
        Opacity = 0;
        Left = screen.WorkingArea.Left;
        Top = screen.WorkingArea.Top;
        Show();
        UpdateLayout();
        var origin = PlaceNearCursor(cursor, screen);
        Activate();
        PlayEnter(origin);
        OnOpened();
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, ReleaseToggle);
    }

    /// <summary>
    /// Выпускает окно из точки щелчка: оно растёт от иконки, всплывает и проявляется
    /// </summary>
    /// <param name="origin">Точка щелчка в координатах экрана, в аппаратно-независимых пикселях</param>
    private void PlayEnter(Point origin)
    {
        if (Content is not FrameworkElement root)
        {
            Opacity = 1;

            return;
        }

        root.RenderTransformOrigin = new Point(Math.Clamp((origin.X - Left) / Math.Max(1, ActualWidth), 0, 1), Math.Clamp((origin.Y - Top) / Math.Max(1, ActualHeight), 0, 1));
        root.RenderTransform = new TransformGroup { Children = { _scale, _shift } };

        var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };
        var grow = TimeSpan.FromMilliseconds(300);
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(ENTER_SCALE, 1, grow) { EasingFunction = ease });
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(ENTER_SCALE, 1, grow) { EasingFunction = ease });
        _shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(16, 0, grow) { EasingFunction = ease });
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    /// <summary>
    /// Возвращает окну обычный вид после анимации
    /// </summary>
    private void ResetMotion()
    {
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _shift.BeginAnimation(TranslateTransform.YProperty, null);
        BeginAnimation(OpacityProperty, null);
        _scale.ScaleX = 1;
        _scale.ScaleY = 1;
        _shift.Y = 0;
        Opacity = 1;
    }

    /// <summary>
    /// Прячет окно, когда фокус ушёл. Щелчок, который его открыл, сюда не попадает
    /// </summary>
    /// <param name="sender">Окно</param>
    /// <param name="e">Аргументы события</param>
    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_ignoreToggle || !IsVisible || _closing)
            return;

        _dismissedAt = Environment.TickCount64;
        Dismiss();
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
    /// <returns>Точка курсора в аппаратно-независимых пикселях экрана</returns>
    private Point PlaceNearCursor(System.Drawing.Point cursor, System.Windows.Forms.Screen screen)
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

        return origin;
    }
}
