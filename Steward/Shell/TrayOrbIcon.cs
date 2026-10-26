using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Shapes;
using Steward.Controls;
using Steward.Models;
using Brush = System.Windows.Media.Brush;

namespace Steward.Shell;

/// <summary>
/// Рисует иконку в трее: диск цвета состояния с бликом. Маленькая иконка должна читаться мгновенно, поэтому сфера из точек остаётся для окон, а здесь сплошной диск, который при выполнении задачи слегка пульсирует
/// </summary>
public static class TrayOrbIcon
{
    /// <summary>
    /// Сторона иконки в пикселях: Windows сама уменьшает её до размера области уведомлений
    /// </summary>
    private const int SIZE = 32;

    /// <summary>
    /// Отступ диска от края в долях стороны, когда он не пульсирует
    /// </summary>
    private const double BASE_INSET = 0.06;

    /// <summary>
    /// Насколько диск сжимается на самой пульсации, в долях стороны
    /// </summary>
    private const double PULSE_INSET = 0.07;

    /// <summary>
    /// Скорость пульсации: радиан в секунду
    /// </summary>
    private const double PULSE_SPEED = 5;

    /// <summary>
    /// Рисует иконку на текущий момент
    /// </summary>
    /// <param name="kind">Вид сферы</param>
    /// <param name="brush">Цвет точек</param>
    /// <returns>Иконка, которую нужно освободить</returns>
    public static Icon Render(ActivityKindEnum kind, Brush brush)
    {
        var target = new RenderTargetBitmap(SIZE, SIZE, 96, 96, PixelFormats.Pbgra32);
        target.Render(Disk(kind, brush));

        var pixels = new byte[SIZE * SIZE * 4];
        target.CopyPixels(pixels, SIZE * 4, 0);

        using var bitmap = new Bitmap(SIZE, SIZE, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        var data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, SIZE, SIZE), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
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
    /// Рисует диск с бликом. У выполняющейся задачи диск «дышит»: чуть сжимается и расширяется
    /// </summary>
    /// <param name="kind">Вид сферы: от него зависит только пульсация</param>
    /// <param name="brush">Цвет состояния</param>
    /// <returns>Элемент с готовой раскладкой</returns>
    private static Grid Disk(ActivityKindEnum kind, Brush brush)
    {
        var pulse = kind == ActivityKindEnum.Working ? 0.5 + 0.5 * Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * PULSE_SPEED) : 0;
        var inset = SIZE * (BASE_INSET + PULSE_INSET * pulse);
        var root = new Grid { Width = SIZE, Height = SIZE };

        root.Children.Add(new Ellipse { Fill = brush, Margin = new Thickness(inset) });
        root.Children.Add(new Ellipse
        {
            Fill = (Brush)System.Windows.Application.Current.Resources["TextBrush"],
            Opacity = 0.3,
            Width = SIZE * 0.34,
            Height = SIZE * 0.26,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(SIZE * 0.2 + inset * 0.5, SIZE * 0.14 + inset * 0.5, 0, 0),
        });
        root.Measure(new System.Windows.Size(SIZE, SIZE));
        root.Arrange(new Rect(0, 0, SIZE, SIZE));

        return root;
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