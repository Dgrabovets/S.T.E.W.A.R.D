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
        var analysis = new LessonAnalysis("Баланс", "Обзор", ["Пройдено"], [new AnalysisTopic("Прогрессия", "Растёт по ступеням")], ["Баланс это согласованность"], ["Решили считать баланс по таблице"], [new AnalysisTask("Сдать таблицу", string.Empty)]);
        var (model, _) = Open(analysis, "== Расшифровка ==\n[00:00] Преподаватель: Начинаем.");

        Assert.Equal(["Пройдено", "Задания", "Темы", "Решения", "Кто говорил"], model.ReaderTabs.Select(tab => tab.Title).ToArray());
        Assert.True(model.IsCoveredTab);
        Assert.False(model.IsTopicsTab);

        model.ReaderTabs.Single(tab => tab.Title == "Решения").SelectCommand.Execute(null);

        Assert.True(model.IsActionsTab);
        Assert.False(model.IsCoveredTab);
        Assert.Equal("Решения", model.ReaderTabs.Single(tab => tab.IsActive).Title);
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

    /// <summary>
    /// У подробного разбора задачи показывает вкладка «Задания», а раздел называется «Решения»; у короткого резюме из MTS Link задачи остаются в разделе «Решения и задачи»
    /// </summary>
    [Fact]
    public void Tasks_are_listed_once_under_the_assignments_tab_and_stay_in_the_brief_summary()
    {
        var detailed = new LessonAnalysis("Баланс", string.Empty, [], [], [], ["Решили"], [new AnalysisTask("Сдать таблицу", string.Empty)]);
        var (withAnalysis, _) = Open(detailed);

        Assert.False(withAnalysis.HasReadingTasks);
        Assert.Equal(["Задания", "Решения"], withAnalysis.ReaderTabs.Select(tab => tab.Title).ToArray());

        var (brief, _) = Open(null, "== Резюме ==\nТема встречи: Баланс\n\nЗадачи:\n• Сдать таблицу\n\n== Расшифровка ==");

        Assert.True(brief.HasReadingTasks);
        Assert.Contains("Решения и задачи", brief.ReaderTabs.Select(tab => tab.Title));
    }
}

