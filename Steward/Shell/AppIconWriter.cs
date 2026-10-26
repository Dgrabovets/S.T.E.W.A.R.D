using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Steward.Controls;
using Steward.Models;
using Brush = System.Windows.Media.Brush;

namespace Steward.Shell;

/// <summary>
/// Собирает файл значка приложения: сфера из точек на тёмной плитке, несколько размеров в одном файле. Запускается командой <c>Steward.exe --snapshot файл.ico appicon</c>
/// </summary>
public static class AppIconWriter
{
    /// <summary>
    /// Размеры, которые Windows берёт для панели задач, меню и проводника
    /// </summary>
    private static readonly int[] Sizes = [16, 24, 32, 48, 64, 128, 256];

    /// <summary>
    /// С какого размера значок хранится как PNG
    /// </summary>
    private const int PNG_FROM = 256;

    /// <summary>
    /// Рисует значок всех размеров и записывает его в файл
    /// </summary>
    /// <param name="path">Куда сохранить файл .ico</param>
    public static void Write(string path)
    {
        var images = Sizes.Select(Render).ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write((short)0);
        writer.Write((short)1);
        writer.Write((short)images.Count);

        var offset = 6 + 16 * images.Count;
        for (var index = 0; index < images.Count; index++)
        {
            var size = Sizes[index];
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((short)1);
            writer.Write((short)32);
            writer.Write(images[index].Length);
            writer.Write(offset);
            offset += images[index].Length;
        }

        foreach (var image in images)
            writer.Write(image);
    }

    /// <summary>
    /// Рисует значок одного размера
    /// </summary>
    /// <param name="size">Сторона в пикселях</param>
    /// <returns>Байты PNG</returns>
    private static byte[] Render(int size)
    {
        var resources = System.Windows.Application.Current.Resources;
        var small = size <= 32;
        var tile = new Border
        {
            Width = size,
            Height = size,
            Background = (Brush)resources["WindowBrush"],
            BorderBrush = (Brush)resources["LineStrongBrush"],
            BorderThickness = new Thickness(Math.Max(1, size / 64.0)),
            CornerRadius = new CornerRadius(size * 0.22),
            Padding = new Thickness(size * 0.1),
            Child = new ActivityOrb
            {
                Kind = ActivityKindEnum.Working,
                Foreground = (Brush)resources["AccentTextBrush"],
                Stride = small ? 2 : 1,
                DotScale = small ? 2.1 : Math.Max(1.4, size / 56.0),
            },
        };
        tile.Measure(new System.Windows.Size(size, size));
        tile.Arrange(new Rect(0, 0, size, size));

        var target = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        target.Render(tile);

        return size >= PNG_FROM ? Png(target) : Dib(target, size);
    }

    /// <summary>
    /// Кодирует картинку в PNG: так значок хранится в самом крупном размере
    /// </summary>
    /// <param name="target">Нарисованная картинка</param>
    /// <returns>Байты PNG</returns>
    private static byte[] Png(RenderTargetBitmap target)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));
        using var buffer = new MemoryStream();
        encoder.Save(buffer);

        return buffer.ToArray();
    }

    /// <summary>
    /// Кодирует картинку в старый формат значка (BMP без заголовка файла с маской): его понимают все версии Windows и средства сборки
    /// </summary>
    /// <param name="target">Нарисованная картинка</param>
    /// <param name="size">Сторона в пикселях</param>
    /// <returns>Байты записи значка</returns>
    private static byte[] Dib(RenderTargetBitmap target, int size)
    {
        var pixels = new byte[size * size * 4];
        target.CopyPixels(pixels, size * 4, 0);

        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer);
        writer.Write(40);
        writer.Write(size);
        writer.Write(size * 2);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(0);
        writer.Write(size * size * 4 + MaskBytes(size));
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        for (var row = size - 1; row >= 0; row--)
        {
            for (var column = 0; column < size; column++)
            {
                var at = (row * size + column) * 4;
                var alpha = pixels[at + 3];
                writer.Write((byte)(alpha == 0 ? 0 : Math.Min(255, pixels[at] * 255 / alpha)));
                writer.Write((byte)(alpha == 0 ? 0 : Math.Min(255, pixels[at + 1] * 255 / alpha)));
                writer.Write((byte)(alpha == 0 ? 0 : Math.Min(255, pixels[at + 2] * 255 / alpha)));
                writer.Write(alpha);
            }
        }

        writer.Write(new byte[MaskBytes(size)]);

        return buffer.ToArray();
    }

    /// <summary>
    /// Размер маски прозрачности: один бит на точку, строка добивается до четырёх байтов
    /// </summary>
    /// <param name="size">Сторона в пикселях</param>
    /// <returns>Число байтов маски</returns>
    private static int MaskBytes(int size) => (size + 31) / 32 * 4 * size;
}