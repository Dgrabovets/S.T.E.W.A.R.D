using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Steward.Shell;

/// <summary>
/// Разворачивает окно без системной рамки ровно в рабочую область монитора, на котором оно стоит: не под панель задач и не за края экрана. Без этого Windows делает такое окно больше экрана на толщину рамки, и края с низом (кнопка «Журнал») обрезаются
/// </summary>
public static class WindowMaximizer
{
    /// <summary>
    /// Сообщение Windows «какие размеры окну можно принимать»
    /// </summary>
    private const int WM_GETMINMAXINFO = 0x0024;

    /// <summary>
    /// Брать ближайший монитор, если окно ни на одном
    /// </summary>
    private const int MONITOR_DEFAULTTONEAREST = 2;

    /// <summary>
    /// Подключает расчёт размеров развёрнутого окна. Вызывать до показа окна или в <c>SourceInitialized</c>
    /// </summary>
    /// <param name="window">Окно</param>
    public static void Attach(Window window)
    {
        if (TryHook(window))
            return;

        window.SourceInitialized += (_, _) => TryHook(window);
    }

    /// <summary>
    /// Рабочая область монитора, на котором стоит окно, в единицах WPF. Пока у окна нет дескриптора, берётся основной монитор
    /// </summary>
    /// <param name="window">Окно</param>
    /// <returns>Рабочая область без панели задач</returns>
    public static Rect WorkArea(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !TryMonitor(handle, out _, out var work))
            return SystemParameters.WorkArea;

        var dpi = VisualTreeHelper.GetDpi(window);

        return new Rect(work.X / dpi.DpiScaleX, work.Y / dpi.DpiScaleY, work.Width / dpi.DpiScaleX, work.Height / dpi.DpiScaleY);
    }

    /// <summary>
    /// На сколько развёрнутое окно вылезло за рабочую область монитора, в единицах WPF. Обычно ноль: размеры задаёт <see cref="Attach"/>. Отступ нужен на случай, если Windows всё же поставила окно шире
    /// </summary>
    /// <param name="window">Развёрнутое окно</param>
    /// <returns>Отступы со всех сторон или нули, если окно в рабочей области</returns>
    public static Thickness Insets(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !TryMonitor(handle, out _, out var work) || !GetWindowRect(handle, out var rect))
            return new Thickness(0);

        var dpi = VisualTreeHelper.GetDpi(window);

        return WindowFit.MaximizedInsets(new Int32Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top), work, dpi.DpiScaleX, dpi.DpiScaleY);
    }

    /// <summary>
    /// Ставит обработчик сообщений окна, если дескриптор уже есть
    /// </summary>
    /// <param name="window">Окно</param>
    /// <returns>
    /// <see langword="true"/>, если обработчик поставлен
    /// </returns>
    private static bool TryHook(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || HwndSource.FromHwnd(handle) is not { } source)
            return false;

        source.AddHook(Hook);

        return true;
    }

    /// <summary>
    /// Отвечает на вопрос Windows о размерах: развёрнутое окно занимает рабочую область своего монитора. Остальные поля (наименьший размер) заполняет сам WPF, поэтому сообщение не помечается обработанным
    /// </summary>
    /// <param name="hwnd">Дескриптор окна</param>
    /// <param name="msg">Номер сообщения</param>
    /// <param name="wParam">Первый параметр</param>
    /// <param name="lParam">Указатель на <c>MINMAXINFO</c></param>
    /// <param name="handled">Сообщение обработано</param>
    /// <returns>Ноль</returns>
    private static IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_GETMINMAXINFO || lParam == IntPtr.Zero || !TryMonitor(hwnd, out var monitor, out var work))
            return IntPtr.Zero;

        var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var bounds = WindowFit.MaximizedBounds(monitor, work);
        info.MaxPosition = new NativePoint { X = bounds.X, Y = bounds.Y };
        info.MaxSize = new NativePoint { X = bounds.Width, Y = bounds.Height };
        Marshal.StructureToPtr(info, lParam, false);

        return IntPtr.Zero;
    }

    /// <summary>
    /// Находит монитор окна и его рабочую область
    /// </summary>
    /// <param name="handle">Дескриптор окна</param>
    /// <param name="monitor">Весь монитор в пикселях</param>
    /// <param name="work">Рабочая область в пикселях</param>
    /// <returns>
    /// <see langword="true"/>, если монитор найден
    /// </returns>
    private static bool TryMonitor(IntPtr handle, out Int32Rect monitor, out Int32Rect work)
    {
        monitor = default;
        work = default;
        var found = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (found == IntPtr.Zero || !GetMonitorInfo(found, ref info))
            return false;

        monitor = info.Monitor.ToInt32Rect();
        work = info.Work.ToInt32Rect();

        return work.Width > 0 && work.Height > 0;
    }

    /// <summary>
    /// Находит монитор, на котором стоит окно
    /// </summary>
    /// <param name="handle">Дескриптор окна</param>
    /// <param name="flags">Что вернуть, если окно ни на одном мониторе</param>
    /// <returns>Дескриптор монитора</returns>
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

    /// <summary>
    /// Читает границы монитора и его рабочей области
    /// </summary>
    /// <param name="monitor">Дескриптор монитора</param>
    /// <param name="info">Куда записать данные</param>
    /// <returns>
    /// <see langword="true"/> при успехе
    /// </returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    /// <summary>
    /// Читает прямоугольник окна в пикселях экрана
    /// </summary>
    /// <param name="handle">Дескриптор окна</param>
    /// <param name="rect">Прямоугольник окна</param>
    /// <returns>
    /// <see langword="true"/> при успехе
    /// </returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);

    /// <summary>
    /// Точка или размер в пикселях, как в Windows
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        /// <summary>
        /// По горизонтали
        /// </summary>
        public int X;

        /// <summary>
        /// По вертикали
        /// </summary>
        public int Y;
    }

    /// <summary>
    /// Прямоугольник в пикселях, как в Windows: края, а не размер
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        /// <summary>
        /// Левый край
        /// </summary>
        public int Left;

        /// <summary>
        /// Верхний край
        /// </summary>
        public int Top;

        /// <summary>
        /// Правый край
        /// </summary>
        public int Right;

        /// <summary>
        /// Нижний край
        /// </summary>
        public int Bottom;

        /// <summary>
        /// Переводит края в положение и размер
        /// </summary>
        /// <returns>Прямоугольник WPF в пикселях</returns>
        public readonly Int32Rect ToInt32Rect() => new(Left, Top, Math.Max(0, Right - Left), Math.Max(0, Bottom - Top));
    }

    /// <summary>
    /// Ответ на <c>WM_GETMINMAXINFO</c>: размеры, которые окно может принимать
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        /// <summary>
        /// Не используется
        /// </summary>
        public NativePoint Reserved;

        /// <summary>
        /// Размер развёрнутого окна
        /// </summary>
        public NativePoint MaxSize;

        /// <summary>
        /// Положение развёрнутого окна относительно монитора
        /// </summary>
        public NativePoint MaxPosition;

        /// <summary>
        /// Наименьший размер при растягивании
        /// </summary>
        public NativePoint MinTrackSize;

        /// <summary>
        /// Наибольший размер при растягивании
        /// </summary>
        public NativePoint MaxTrackSize;
    }

    /// <summary>
    /// Данные монитора из <c>GetMonitorInfo</c>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        /// <summary>
        /// Размер структуры в байтах, его заполняет вызывающий
        /// </summary>
        public int Size;

        /// <summary>
        /// Весь монитор
        /// </summary>
        public NativeRect Monitor;

        /// <summary>
        /// Рабочая область без панели задач
        /// </summary>
        public NativeRect Work;

        /// <summary>
        /// Признаки монитора
        /// </summary>
        public int Flags;
    }
}
