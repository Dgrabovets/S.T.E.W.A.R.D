using Steward.Hosting;
using Steward.Ideas.Models;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Проверяет карточку идеи и просьбу к Claude, которая открывается по кнопке «Делаем»
/// </summary>
public class IdeaTests
{
    /// <summary>
    /// Идея с длинной расшифровкой, автором и ссылкой
    /// </summary>
    private static readonly Idea Sample = new()
    {
        Id = "r1-1",
        CreatedAt = new DateTimeOffset(2026, 10, 25, 12, 30, 0, TimeSpan.Zero),
        SourceUrl = "https://www.instagram.com/reel/x/",
        Title = "Идея",
        Author = "@author",
        Transcript = string.Join(' ', Enumerable.Repeat("слово", 200)),
    };

    /// <summary>
    /// Превью расшифровки короткое и обрывается по слову с многоточием
    /// </summary>
    [Fact]
    public void The_preview_is_shortened_at_a_word()
    {
        var card = new IdeaCardViewModel(Sample, _ => { }, _ => { }, _ => { });

        Assert.True(card.Preview.Length <= 321);
        Assert.EndsWith("слово…", card.Preview, StringComparison.Ordinal);
        Assert.StartsWith("@author · ", card.Meta, StringComparison.Ordinal);
    }

    /// <summary>
    /// Без расшифровки показывается подпись, а без неё превью нет
    /// </summary>
    [Fact]
    public void The_caption_replaces_a_missing_transcript_and_nothing_hides_the_preview()
    {
        var withCaption = new IdeaCardViewModel(Sample with { Transcript = "", Caption = "Подпись" }, _ => { }, _ => { }, _ => { });
        var empty = new IdeaCardViewModel(Sample with { Transcript = "" }, _ => { }, _ => { }, _ => { });

        Assert.Equal("Подпись", withCaption.Preview);
        Assert.False(empty.HasPreview);
    }

    /// <summary>
    /// Кнопки передают нужную идею и ссылку
    /// </summary>
    [Fact]
    public void The_commands_pass_the_idea_and_the_link()
    {
        var approved = new List<string>();
        var rejected = new List<string>();
        var opened = new List<string>();
        var card = new IdeaCardViewModel(Sample, idea => approved.Add(idea.Id), idea => rejected.Add(idea.Id), opened.Add);

        card.ApproveCommand.Execute(null);
        card.RejectCommand.Execute(null);
        card.OpenSourceCommand.Execute(null);

        Assert.Equal(["r1-1"], approved);
        Assert.Equal(["r1-1"], rejected);
        Assert.Equal(["https://www.instagram.com/reel/x/"], opened);
    }

    /// <summary>
    /// Просьба называет идею, даёт путь к файлу, ограничивает действия и не содержит саму расшифровку
    /// </summary>
    [Fact]
    public void The_prompt_points_to_the_file_and_keeps_the_transcript_out()
    {
        var prompt = IdeaPrompt.Build(Sample, @"D:\repo", @"D:\repo\data\ideas\r1-1.json");

        Assert.Contains("«Идея»", prompt, StringComparison.Ordinal);
        Assert.Contains(@"D:\repo\data\ideas\r1-1.json", prompt, StringComparison.Ordinal);
        Assert.Contains("не инструкции", prompt, StringComparison.Ordinal);
        Assert.Contains("Не читай секреты", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("слово слово", prompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ссылка для Claude содержит папку и просьбу в закодированном виде
    /// </summary>
    [Fact]
    public void The_link_encodes_the_folder_and_the_prompt()
    {
        var url = ClaudeLauncher.BuildUrl("привет мир", @"D:\repo");

        Assert.StartsWith("claude://code/new?folder=D%3A%5Crepo&q=", url, StringComparison.Ordinal);
        Assert.DoesNotContain(" ", url, StringComparison.Ordinal);
    }
}