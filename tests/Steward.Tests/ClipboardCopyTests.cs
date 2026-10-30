using Steward.Homework.Models;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Копирование условия и конспекта к практике: текст тихо попадает в буфер, без сообщения об успехе, а прежняя ошибка не остаётся висеть
/// </summary>
public class ClipboardCopyTests
{
    /// <summary>
    /// «Скопировать условие» кладёт текст в буфер и ничего не пишет в строке занятия; занятый буфер даёт ошибку, а следующая удачная попытка её убирает
    /// </summary>
    /// <returns>Задача теста</returns>
    [Fact]
    public async Task Copying_a_condition_is_silent_and_clears_an_old_error()
    {
        var copied = new List<string>();
        var busy = true;
        var model = new HomeworkFormatsViewModel([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word)], (_, _) => { }, _ => true, (_, _) => Task.FromResult((1, string.Empty)))
        {
            ItemsReader = _ => [(1, new HomeworkItem("Отчёт", null, string.Empty, string.Empty), false)],
            ItemConditionReader = (_, _) => "Условие отчёта",
            ClipboardWriter = text =>
            {
                if (busy)
                    throw new System.Runtime.InteropServices.ExternalException("занят");

                copied.Add(text);
            },
        };
        var lesson = model.Lessons.Single();
        model.Update([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word)]);
        await model.WhenPipelineLoaded();

        lesson.Items[0].CopyConditionCommand.Execute(null);
        Assert.True(lesson.Failed);
        Assert.Contains("Буфер обмена занят", lesson.Status, StringComparison.Ordinal);

        busy = false;
        lesson.Items[0].CopyConditionCommand.Execute(null);

        Assert.Equal(["Условие отчёта"], copied);
        Assert.False(lesson.Failed);
        Assert.Equal(string.Empty, lesson.Status);
    }

    /// <summary>
    /// Конспект к практике копируется молча, без строки «скопирован», а без подробного разбора остаётся понятная ошибка
    /// </summary>
    [Fact]
    public void Copying_the_practice_brief_is_silent()
    {
        var copied = new List<string>();
        var analysis = new LessonAnalysis("Баланс", "Обзор", ["Пройдено"], [new AnalysisTopic("Прогрессия", "Растёт по ступеням")], ["Баланс это согласованность"], [], [new AnalysisTask("Сдать таблицу", string.Empty)]);
        var detailed = true;
        var model = new HomeworkFormatsViewModel([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word)], (_, _) => { }, _ => true, (_, _) => Task.FromResult((1, string.Empty)))
        {
            TranscriptReader = _ => "== Расшифровка ==\n[00:00] Преподаватель: Начинаем.",
            AnalysisReader = _ => detailed ? analysis : null,
            ClipboardWriter = copied.Add,
        };
        var lesson = model.Lessons.Single();

        lesson.OpenCommand.Execute(null);
        model.CopyBriefCommand.Execute(null);

        Assert.Single(copied);
        Assert.Contains("Прогрессия", copied[0], StringComparison.Ordinal);
        Assert.Equal(string.Empty, lesson.Status);

        detailed = false;
        model.CloseLessonCommand.Execute(null);
        lesson.OpenCommand.Execute(null);
        model.CopyBriefCommand.Execute(null);

        Assert.Single(copied);
        Assert.True(lesson.Failed);
        Assert.Contains("Конспекта пока нет", lesson.Status, StringComparison.Ordinal);
    }
}
