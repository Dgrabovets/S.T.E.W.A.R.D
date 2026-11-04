using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Steward.Shell;

/// <summary>
/// Просит Windows 11 скруглить углы окна. Окна без прозрачности рисуются в разы быстрее прозрачных, поэтому скругляет их система, а не приложение
/// </summary>
public static class WindowCorners
{
    /// <summary>
    /// Номер параметра «вид углов окна» в DWM
    /// </summary>
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

    /// <summary>
    /// Значение «скруглять»
    /// </summary>
    private const int DWMWCP_ROUND = 2;

    /// <summary>
    /// Включает скругление у окна. На системах без этой возможности вызов ничего не меняет
    /// </summary>
    /// <param name="window">Окно, у которого уже создан дескриптор</param>
    public static void Round(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return;

        var preference = DWMWCP_ROUND;
        _ = DwmSetWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
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
