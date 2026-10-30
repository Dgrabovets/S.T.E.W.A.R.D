using Steward.Homework.Models;
using Steward.Models;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Вкладки читалки расшифровки: разделы разбора показываются по одному, пустые вкладки не появляются, а открытие другого занятия возвращает первую вкладку
/// </summary>
public class ReaderTabsTests
{
    /// <summary>
    /// Собирает список с одним занятием, у которого есть подробный разбор
    /// </summary>
    /// <param name="analysis">Разбор занятия или <see langword="null"/>, если подробного разбора нет</param>
    /// <param name="transcript">Текст расшифровки: реплики дают раздел «Кто говорил», без них его нет</param>
    /// <returns>Список занятий и его занятие</returns>
    private static (HomeworkFormatsViewModel Model, LessonFormatViewModel Lesson) Open(LessonAnalysis? analysis, string transcript = "== Расшифровка ==")
    {
        var model = new HomeworkFormatsViewModel([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word)], (_, _) => { }, _ => true, (_, _) => Task.FromResult((1, string.Empty)))
        {
            TranscriptReader = _ => transcript,
            AnalysisReader = _ => analysis,
        };
        var lesson = model.Lessons.Single();
        lesson.OpenCommand.Execute(null);

        return (model, lesson);
    }

    /// <summary>
    /// Вкладки есть только у разделов с содержимым, по умолчанию показана первая, а выбор другой переключает раздел и подсветку
    /// </summary>
    [Fact]
    public void Only_sections_with_content_get_tabs_and_selecting_one_switches_the_section()
    {
        var analysis = new LessonAnalysis("Баланс", "Обзор", ["Пройдено"], [new AnalysisTopic("Прогрессия", "Растёт по ступеням")], ["Баланс это согласованность"], [], [new AnalysisTask("Сдать таблицу", string.Empty)]);
        var (model, _) = Open(analysis, "== Расшифровка ==\n[00:00] Преподаватель: Начинаем.");

        Assert.Equal(["Пройдено", "Темы", "Решения и задачи", "Кто говорил"], model.ReaderTabs.Select(tab => tab.Title).ToArray());
        Assert.True(model.IsCoveredTab);
        Assert.False(model.IsTopicsTab);

        model.ReaderTabs.Single(tab => tab.Title == "Решения и задачи").SelectCommand.Execute(null);

        Assert.True(model.IsActionsTab);
        Assert.False(model.IsCoveredTab);
        Assert.Equal("Решения и задачи", model.ReaderTabs.Single(tab => tab.IsActive).Title);
    }

    /// <summary>
    /// Без разбора вкладок нет, а раздел, которого нет у нового занятия, заменяется первым доступным
    /// </summary>
    [Fact]
    public void Without_content_there_are_no_tabs_and_a_missing_section_falls_back_to_the_first_one()
    {
        var (empty, _) = Open(null, string.Empty);

        Assert.False(empty.HasReaderTabs);
        Assert.False(empty.IsTopicsTab);

        var onlyTopics = new LessonAnalysis("Баланс", string.Empty, [], [new AnalysisTopic("Прогрессия", "Растёт по ступеням")], [], [], []);
        var (model, lesson) = Open(onlyTopics);

        Assert.Equal(ReaderTabEnum.Topics, model.ActiveReaderTab);
        Assert.True(model.IsTopicsTab);

        model.CloseLessonCommand.Execute(null);
        lesson.OpenCommand.Execute(null);

        Assert.Equal(ReaderTabEnum.Topics, model.ActiveReaderTab);
    }
}
