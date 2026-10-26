using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Steward.Controls;
using Steward.Models;
using Brush = System.Windows.Media.Brush;

namespace Steward.Shell;

/// <summary>
/// Рисует иконку в трее: та же плитка со сферой, что у значка приложения, вид и цвет сферы по состоянию
/// </summary>
public static class TrayOrbIcon
{
    /// <summary>
    /// Сторона иконки в пикселях: Windows сама уменьшает её до размера области уведомлений
    /// </summary>
    private const int SIZE = 32;

    /// <summary>
    /// Рисует иконку на текущий момент
    /// </summary>
    /// <param name="kind">Вид сферы</param>
    /// <param name="brush">Цвет точек</param>
    /// <returns>Иконка, которую нужно освободить</returns>
    public static Icon Render(ActivityKindEnum kind, Brush brush)
    {
        var target = new RenderTargetBitmap(SIZE, SIZE, 96, 96, PixelFormats.Pbgra32);
        target.Render(AppIconWriter.Tile(SIZE, kind, brush));

        var pixels = new byte[SIZE * SIZE * 4];
        target.CopyPixels(pixels, SIZE * 4, 0);

        using var bitmap = new Bitmap(SIZE, SIZE, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, SIZE, SIZE), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        try
        {
            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);

            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    /// <summary>
    /// Удаляет значок, созданный через <c>GetHicon</c>
    /// </summary>
    /// <param name="handle">Дескриптор значка</param>
    /// <returns>
    /// <see langword="true"/>, если значок удалён
    /// </returns>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}