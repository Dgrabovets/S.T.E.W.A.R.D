using Steward.Reels.Services;

namespace Steward.Reels.Tests;

/// <summary>
/// Проверяет, как название идеи берётся из подписи, расшифровки или автора
/// </summary>
public class IdeaTitleTests
{
    /// <summary>
    /// Первая строка подписи важнее расшифровки
    /// </summary>
    [Fact]
    public void The_first_caption_line_wins() => Assert.Equal("Заголовок", IdeaTitle.Build("Заголовок\nвторая строка", "Речь.", "@a"));

    /// <summary>
    /// Без подписи берётся первое предложение расшифровки
    /// </summary>
    [Fact]
    public void Without_a_caption_the_first_sentence_of_the_transcript_is_used() => Assert.Equal("Первая мысль", IdeaTitle.Build("", "Первая мысль. Вторая мысль.", "@a"));

    /// <summary>
    /// Без подписи и речи остаётся автор, а если нет и его, слово «Рилс»
    /// </summary>
    [Fact]
    public void Without_anything_the_author_or_a_plain_word_is_used()
    {
        Assert.Equal("Рилс @a", IdeaTitle.Build("", "", "@a"));
        Assert.Equal("Рилс", IdeaTitle.Build("", "", ""));
    }

    /// <summary>
    /// Длинное название обрезается по слову с многоточием
    /// </summary>
    [Fact]
    public void A_long_title_is_cut_at_a_word()
    {
        var title = IdeaTitle.Build(string.Join(' ', Enumerable.Repeat("слово", 40)), "", "");

        Assert.EndsWith("…", title, StringComparison.Ordinal);
        Assert.True(title.Length <= 81);
    }
}