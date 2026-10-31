using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Models;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Место в расшифровке, где сказано задание: поиск цитаты по строкам и вкладка «Где сказано» в читалке
/// </summary>
public class QuoteTests
{
    /// <summary>
    /// Расшифровка для проверок
    /// </summary>
    private const string TRANSCRIPT = "== Расшифровка ==\n[10:02] Преподаватель: Сегодня разобрали баланс.\n[10:40] Преподаватель: Значит, домашка на следующую неделю.\n[10:42] Преподаватель: Покажите прототип\nменю до пятницы, хотя бы на бумаге.\n[10:50] Студент: Хорошо.\n[11:20] Преподаватель: На сегодня всё.";

    /// <summary>
    /// Цитата на двух строках находится, отмечаются только эти строки, вокруг них показываются соседние, а отсутствующая или пустая цитата даёт пустой отрывок
    /// </summary>
    [Fact]
    public void A_quote_over_two_lines_is_found_and_only_those_lines_are_marked()
    {
        var excerpt = EvidenceCheck.Excerpt(TRANSCRIPT, "«покажите  прототип меню до пятницы»", 1);

        Assert.Equal(["[10:40] Преподаватель: Значит, домашка на следующую неделю.", "[10:42] Преподаватель: Покажите прототип", "меню до пятницы, хотя бы на бумаге.", "[10:50] Студент: Хорошо."], excerpt.Select(line => line.Text).ToArray());
        Assert.Equal([false, true, true, false], excerpt.Select(line => line.Hit).ToArray());
        Assert.Empty(EvidenceCheck.Excerpt(TRANSCRIPT, "совсем другое задание"));
        Assert.Empty(EvidenceCheck.Excerpt(TRANSCRIPT, "   "));
    }

    /// <summary>
    /// Расшифровка, открытая из карточки задания, показывает вкладку «Где сказано» первой и выбранной, а открытая из списка занятий её не показывает
    /// </summary>
    [Fact]
    public void Opening_the_transcript_from_a_task_shows_where_it_was_said()
    {
        var lesson = new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19));
        var model = new HomeworkFormatsViewModel([(lesson, HomeworkFormatEnum.Word)], (_, _) => { }, _ => true, (_, _) => Task.FromResult((1, string.Empty)))
        {
            TranscriptReader = _ => TRANSCRIPT,
            ItemsReader = _ => [(1, new HomeworkItem("Показать прототип меню", null, string.Empty, string.Empty, Evidence: "Покажите прототип меню до пятницы", Confidence: "high"), false)],
        };
        model.Update([(lesson, HomeworkFormatEnum.Word)]);
        var row = model.Lessons.Single();

        row.OpenCommand.Execute(null);

        Assert.Null(model.Quote);
        Assert.DoesNotContain(model.ReaderTabs, tab => tab.Tab == ReaderTabEnum.Quote);

        var item = new LessonItemViewModel(1, new HomeworkItem("Показать прототип меню", null, string.Empty, string.Empty, Evidence: "Покажите прототип меню до пятницы", Confidence: "high"), false, _ => { }, _ => { }, actions: new ItemCardActions { OpenTranscript = it => model.OpenFromItem(row, it) });
        item.OpenTranscriptCommand.Execute(null);

        Assert.Equal(ReaderTabEnum.Quote, model.ActiveReaderTab);
        Assert.Equal("Где сказано", model.ReaderTabs[0].Title);
        Assert.True(model.IsQuoteTab);
        Assert.Equal(2, model.Quote!.Lines.Count(line => line.IsHit));

        var withoutQuote = new LessonItemViewModel(2, new HomeworkItem("Старое задание", null, string.Empty, string.Empty), false, _ => { }, _ => { }, actions: new ItemCardActions { OpenTranscript = it => model.OpenFromItem(row, it) });
        withoutQuote.OpenTranscriptCommand.Execute(null);

        Assert.True(model.Quote!.HasNote);
        Assert.False(model.Quote.HasLines);
    }
}
