using Steward.Reels.Services;

namespace Steward.Reels.Tests;

/// <summary>
/// Проверяет разбор ответа Claude и чистку текста от смайликов и скрытых символов
/// </summary>
public class IdeaAnalysisTests
{
    /// <summary>
    /// Ответ по форме превращается в название и разбор, смайлики и скрытые символы пропадают
    /// </summary>
    [Fact]
    public void A_well_formed_answer_is_split_and_cleaned()
    {
        var analysis = ClaudeIdeaAnalyzer.Parse("НАЗВАНИЕ: 🚀 Звук по окончании​ задачи.\nРАЗБОР:\nСуть: хук.\n\n\nПлюсы: просто 👍");

        Assert.NotNull(analysis);
        Assert.Equal("Звук по окончании задачи", analysis.Title);
        Assert.Equal("Суть: хук.\n\nПлюсы: просто", analysis.Summary.Replace("\n\n\n", "\n\n", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ответ без меток или без названия не годится: останется название из подписи
    /// </summary>
    [Fact]
    public void An_answer_without_marks_or_a_title_is_rejected()
    {
        Assert.Null(ClaudeIdeaAnalyzer.Parse("Просто текст"));
        Assert.Null(ClaudeIdeaAnalyzer.Parse("НАЗВАНИЕ: \nРАЗБОР: что-то"));
    }

    /// <summary>
    /// Чистка оставляет буквы, цифры, «№» и стрелки, убирает смайлики, флаги, символы нулевой ширины и выбор начертания
    /// </summary>
    [Fact]
    public void The_cleaner_keeps_text_and_removes_pictograms_and_hidden_characters()
    {
        Assert.Equal("Пункт № 5 → дальше", TextCleaner.CleanLine("Пункт № 5 → дальше‍️"));
        Assert.Equal("Огонь", TextCleaner.CleanLine("🔥 Огонь 🇷🇺 ❤️"));
    }

    /// <summary>
    /// Одна и та же ссылка с разным видом даёт один ключ, а у разных рилсов ключи разные
    /// </summary>
    [Fact]
    public void Links_to_the_same_reel_share_a_key()
    {
        Assert.Equal(ReelLinks.Key("https://www.instagram.com/reel/AbC/"), ReelLinks.Key("https://instagram.com/user/reels/AbC/?igsh=1"));
        Assert.NotEqual(ReelLinks.Key("https://www.instagram.com/reel/AbC/"), ReelLinks.Key("https://www.instagram.com/reel/abc/"));
    }
}
