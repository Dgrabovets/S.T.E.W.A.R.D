using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Steward.Shell;

/// <summary>
/// Анимация окна «как на Mac»: окно вытекает из иконки в трее и втягивается обратно. У окна без прозрачности нельзя масштабировать содержимое, поэтому рисуется его снимок в лёгком прозрачном окне-призраке, а настоящее окно на это время скрыто
/// </summary>
public static class WindowGenie
{
    /// <summary>
    /// Номер параметра «спрятать окно, не закрывая» в DWM
    /// </summary>
    private const int DWMWA_CLOAK = 13;

    /// <summary>
    /// Длительность появления, секунд
    /// </summary>
    private const double EXPAND_SECONDS = 0.34;

    /// <summary>
    /// Длительность исчезновения, секунд
    /// </summary>
    private const double COLLAPSE_SECONDS = 0.26;

    /// <summary>
    /// Сторона окна-призрака у иконки, в аппаратно-независимых пикселях
    /// </summary>
    private const double ICON_SIZE = 28;

    /// <summary>
    /// Показывает окно, выпуская его из иконки в трее
    /// </summary>
    /// <param name="window">Окно, которое ещё не видно</param>
    /// <param name="show">Показывает и активирует окно</param>
    public static void Expand(Window window, Action show)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        Cloak(handle, true);
        try
        {
            show();
            window.UpdateLayout();
        }
        catch
        {
            Cloak(handle, false);
            throw;
        }

        var target = new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight);
        if (target.Width < 1 || target.Height < 1)
        {
            Cloak(handle, false);

            return;
        }

        var ghost = BuildGhost(window, Capture(window), IconRect(window));
        ghost.Show();
        Fly(ghost, target, EXPAND_SECONDS, opening: true, () =>
        {
            Cloak(handle, false);
            window.Activate();
            window.Dispatcher.BeginInvoke(DispatcherPriority.Background, ghost.Close);
        });
    }

    /// <summary>
    /// Прячет окно, втягивая его в иконку в трее
    /// </summary>
    /// <param name="window">Окно, которое сейчас видно</param>
    /// <param name="hide">Прячет настоящее окно</param>
    public static void Collapse(Window window, Action hide)
    {
        var source = new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight);
        if (source.Width < 1 || source.Height < 1 || window.WindowState != WindowState.Normal)
        {
            hide();

            return;
        }

        var ghost = BuildGhost(window, Capture(window), source);
        ghost.Show();
        hide();
        Fly(ghost, IconRect(window), COLLAPSE_SECONDS, opening: false, ghost.Close);
    }

    /// <summary>
    /// Квадрат у иконки в трее в аппаратно-независимых пикселях
    /// </summary>
    /// <param name="window">Окно, по чьему масштабу пересчитываются пиксели</param>
    /// <returns>Прямоугольник с центром в точке иконки</returns>
    private static Rect IconRect(Window window)
    {
        var anchor = TrayAnchor.Resolve();
        var scale = VisualTreeHelper.GetDpi(window);
        var x = anchor.X / scale.DpiScaleX;
        var y = anchor.Y / scale.DpiScaleY;

        return new Rect(x - ICON_SIZE / 2, y - ICON_SIZE / 2, ICON_SIZE, ICON_SIZE);
    }

    /// <summary>
    /// Снимает картинку с содержимого окна
    /// </summary>
    /// <param name="window">Окно с готовой раскладкой</param>
    /// <returns>Картинка в размере окна на экране</returns>
    private static BitmapSource Capture(Window window)
    {
        var dpi = VisualTreeHelper.GetDpi(window);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(window.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(window.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32);

        bitmap.Render((Visual)window.Content);
        bitmap.Freeze();

        return bitmap;
    }

    /// <summary>
    /// Создаёт окно-призрак с картинкой: прозрачное, поверх всех, без фокуса и без реакции на мышь
    /// </summary>
    /// <param name="window">Настоящее окно, по нему берётся масштаб</param>
    /// <param name="picture">Снимок окна</param>
    /// <param name="start">Где призрак стоит сначала</param>
    /// <returns>Окно, которое ещё не показано</returns>
    private static Window BuildGhost(Window window, BitmapSource picture, Rect start) => new()
    {
        WindowStyle = WindowStyle.None,
        AllowsTransparency = true,
        Background = System.Windows.Media.Brushes.Transparent,
        ShowInTaskbar = false,
        ShowActivated = false,
        Topmost = true,
        ResizeMode = ResizeMode.NoResize,
        IsHitTestVisible = false,
        Focusable = false,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Left = start.Left,
        Top = start.Top,
        Width = start.Width,
        Height = start.Height,
        Content = new System.Windows.Controls.Image { Source = picture, Stretch = Stretch.Fill, SnapsToDevicePixels = false },
    };

    /// <summary>
    /// Перемещает и растягивает призрак к цели: ширина меняется быстрее высоты, отчего окно сужается воронкой, а прозрачность подстраивается под направление
    /// </summary>
    /// <param name="ghost">Окно-призрак</param>
    /// <param name="end">Куда он приходит</param>
    /// <param name="seconds">Длительность</param>
    /// <param name="opening">
    /// <see langword="true"/>, если окно выходит из иконки, <see langword="false"/>, если уходит в неё
    /// </param>
    /// <param name="completed">Вызывается, когда движение закончилось</param>
    private static void Fly(Window ghost, Rect end, double seconds, bool opening, Action completed)
    {
        var mode = opening ? EasingMode.EaseOut : EasingMode.EaseIn;
        var duration = TimeSpan.FromSeconds(seconds);

        DoubleAnimation Move(double to, int power) => new(to, duration) { EasingFunction = new PowerEase { Power = power, EasingMode = mode } };

        ghost.Opacity = opening ? 0.35 : 1;
        var height = Move(end.Height, 3);
        height.Completed += (_, _) => completed();

        ghost.BeginAnimation(Window.LeftProperty, Move(end.Left, 3));
        ghost.BeginAnimation(Window.TopProperty, Move(end.Top, 3));
        ghost.BeginAnimation(FrameworkElement.WidthProperty, Move(end.Width, opening ? 2 : 4));
        ghost.BeginAnimation(FrameworkElement.HeightProperty, height);
        ghost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(opening ? 1 : 0, TimeSpan.FromSeconds(seconds * (opening ? 0.5 : 0.9))) { EasingFunction = new QuadraticEase { EasingMode = mode } });
    }

    /// <summary>
    /// Прячет окно от глаз, не закрывая его, или возвращает
    /// </summary>
    /// <param name="handle">Дескриптор окна</param>
    /// <param name="cloaked">
    /// <see langword="true"/>, чтобы спрятать
    /// </param>
    private static void Cloak(IntPtr handle, bool cloaked)
    {
        var value = cloaked ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, DWMWA_CLOAK, ref value, sizeof(int));
    }

    /// <summary>
    /// Задаёт параметр оформления окна в диспетчере окон
    /// </summary>
    /// <param name="handle">Дескриптор окна</param>
    /// <param name="attribute">Номер параметра</param>
    /// <param name="value">Значение параметра</param>
    /// <param name="size">Размер значения в байтах</param>
    /// <returns>Код результата, ноль при успехе</returns>
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
}
