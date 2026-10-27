using Steward.Reels.Services;

namespace Steward.Reels.Tests;

/// <summary>
/// Проверяет аргументы yt-dlp: вход в Instagram из браузера добавляется только по настройке
/// </summary>
public class YtDlpArgumentsTests
{
    /// <summary>
    /// Без браузера в настройках ключа входа нет, ссылка идёт последней
    /// </summary>
    [Fact]
    public void Without_a_browser_there_is_no_cookies_option()
    {
        var arguments = YtDlpDownloader.Arguments("https://instagram.com/p/x", "r1", @"D:\work", string.Empty);

        Assert.DoesNotContain("--cookies-from-browser", arguments);
        Assert.Equal("https://instagram.com/p/x", arguments[^1]);
    }

    /// <summary>
    /// С браузером в настройках yt-dlp получает его название ключом входа, лишние пробелы убираются
    /// </summary>
    [Fact]
    public void A_browser_in_the_settings_becomes_the_cookies_option()
    {
        var arguments = YtDlpDownloader.Arguments("https://instagram.com/p/x", "r1", @"D:\work", " firefox ");

        var index = arguments.ToList().IndexOf("--cookies-from-browser");
        Assert.True(index >= 0);
        Assert.Equal("firefox", arguments[index + 1]);
        Assert.Equal("https://instagram.com/p/x", arguments[^1]);
    }

    /// <summary>
    /// Файл cookies важнее браузера: в аргументах только он
    /// </summary>
    [Fact]
    public void A_cookies_file_replaces_the_browser()
    {
        var arguments = YtDlpDownloader.Arguments("https://instagram.com/p/x", "r1", @"D:\work", "firefox", @"D:\secrets\cookies.txt");

        Assert.Contains("--cookies", arguments);
        Assert.DoesNotContain("--cookies-from-browser", arguments);
        Assert.Equal(@"D:\secrets\cookies.txt", arguments[arguments.ToList().IndexOf("--cookies") + 1]);
    }
}
