using System.Text.RegularExpressions;
using System.Xml.Linq;
using Steward.Status.Common;

namespace Steward.Tests;

/// <summary>
/// Проверяет дизайн-систему: контраст пар цветов и то, что цвета, размеры текста и скругления берутся из токенов
/// </summary>
public class DesignTokensTests
{
    /// <summary>
    /// Пространство имён XAML для разбора файлов
    /// </summary>
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>
    /// Все цвета, которые в приложении читаются как текст, и пары, где нужен контраст не меньше 4.5
    /// </summary>
    public static TheoryData<string, string, double> Pairs => new()
    {
        { "Text", "Surface", 4.5 },
        { "Text", "Window", 4.5 },
        { "Muted", "Surface", 4.5 },
        { "Muted", "Window", 4.5 },
        { "Muted", "Raised", 4.5 },
        { "Idle", "Surface", 4.5 },
        { "Ok", "OkSurface", 4.5 },
        { "Attention", "AttentionSurface", 4.5 },
        { "Danger", "DangerSurface", 4.5 },
        { "Danger", "Window", 4.5 },
        { "Danger", "Surface", 4.5 },
        { "OnAccent", "Accent", 4.5 },
        { "OnAccent", "AccentHover", 4.5 },
        { "OnAccent", "AccentPressed", 4.5 },
        { "AccentText", "Window", 4.5 },
        { "AccentText", "Surface", 4.5 },
        { "LineStrong", "Window", 3.0 },
        { "LineStrong", "Surface", 3.0 },
        { "Ok", "Surface", 3.0 },
    };

    /// <summary>
    /// Пары цветов читаются и различаются: текст не ниже 4.5, границы и значки не ниже 3
    /// </summary>
    /// <param name="foreground">Цвет текста или значка</param>
    /// <param name="background">Цвет фона</param>
    /// <param name="minimum">Наименьший допустимый контраст</param>
    [Theory]
    [MemberData(nameof(Pairs))]
    public void Colors_keep_the_contrast(string foreground, string background, double minimum)
    {
        var colors = ReadColors();

        Assert.True(Contrast.Ratio(colors[foreground], colors[background]) >= minimum, $"{foreground} на {background} слабее {minimum}");
    }

    /// <summary>
    /// Шестнадцатеричные цвета записаны только в Themes/Colors.xaml
    /// </summary>
    [Fact]
    public void Hex_colors_live_only_in_the_colors_dictionary()
    {
        var offenders = AppFiles("*.xaml")
            .Where(file => !file.EndsWith(Path.Combine("Themes", "Colors.xaml"), StringComparison.OrdinalIgnoreCase))
            .Where(file => Regex.IsMatch(File.ReadAllText(file), "#[0-9A-Fa-f]{6,8}\\b"))
            .ToArray();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// В коде нет цветов, заданных числами: меню и рисунки берут их из ресурсов
    /// </summary>
    [Fact]
    public void Code_does_not_define_colors_by_numbers()
    {
        var offenders = AppFiles("*.cs")
            .Where(file => Regex.IsMatch(File.ReadAllText(file), "Color\\.From(Rgb|Argb)\\(\\s*\\d+\\s*,\\s*\\d+\\s*,\\s*\\d+"))
            .ToArray();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// Размеры текста в разметке берутся из шкалы, а не числом
    /// </summary>
    [Fact]
    public void Font_sizes_come_from_the_scale()
    {
        var offenders = AppFiles("*.xaml")
            .Where(file => !file.EndsWith(Path.Combine("Themes", "Typography.xaml"), StringComparison.OrdinalIgnoreCase))
            .Where(file => Regex.IsMatch(File.ReadAllText(file), "FontSize=\"\\d"))
            .ToArray();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// Скругления от 4 пикселей и больше берутся из токенов
    /// </summary>
    [Fact]
    public void Corner_radii_come_from_tokens()
    {
        var offenders = AppFiles("*.xaml")
            .Where(file => !file.EndsWith(Path.Combine("Themes", "Metrics.xaml"), StringComparison.OrdinalIgnoreCase))
            .Where(file => Regex.IsMatch(File.ReadAllText(file), "CornerRadius=\"([4-9]|\\d{2,})"))
            .ToArray();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// Читает цвета из словаря темы
    /// </summary>
    /// <returns>Шестнадцатеричная запись по имени без суффикса <c>Color</c></returns>
    private static Dictionary<string, string> ReadColors()
    {
        var path = Path.Combine(AppDirectory(), "Themes", "Colors.xaml");

        return XDocument.Load(path).Descendants()
            .Where(element => element.Name.LocalName == "Color")
            .ToDictionary(
                element => ((string)element.Attribute(Xaml + "Key")!)[..^"Color".Length],
                element => element.Value.Trim(),
                StringComparer.Ordinal);
    }

    /// <summary>
    /// Находит файлы приложения без папок сборки
    /// </summary>
    /// <param name="pattern">Маска имени файла</param>
    /// <returns>Пути к файлам</returns>
    private static IEnumerable<string> AppFiles(string pattern) =>
        Directory.EnumerateFiles(AppDirectory(), pattern, SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    /// <summary>
    /// Папка проекта приложения в репозитории
    /// </summary>
    /// <returns>Путь к папке <c>Steward</c></returns>
    private static string AppDirectory() => Path.Combine(DataLocation.FindRepoRoot()!, "Steward");
}