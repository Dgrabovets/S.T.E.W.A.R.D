using Steward.Reels.Services;

namespace Steward.Reels.Tests;

/// <summary>
/// Проверяет поиск ссылок на рилсы в тексте сообщения
/// </summary>
public class ReelLinksTests
{
    /// <summary>
    /// Находит рилс, пост и ссылку с именем автора, повторы убирает
    /// </summary>
    [Fact]
    public void Find_returns_distinct_instagram_links()
    {
        var links = ReelLinks.Find("Смотри https://www.instagram.com/reel/AbC-123/?igsh=x и https://instagram.com/user.name/p/Zz9/ ещё https://www.instagram.com/reel/AbC-123/");

        Assert.Equal(["https://www.instagram.com/reel/AbC-123/", "https://instagram.com/user.name/p/Zz9/"], links);
    }

    /// <summary>
    /// Другие сайты и обычный текст ссылками не считаются
    /// </summary>
    [Theory]
    [InlineData("привет")]
    [InlineData("https://example.com/reel/abc")]
    [InlineData("https://www.instagram.com/someone/")]
    public void Find_ignores_everything_else(string text) => Assert.Empty(ReelLinks.Find(text));
}