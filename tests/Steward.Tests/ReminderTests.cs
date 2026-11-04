using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Homework.Storage;
using Steward.Preview;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Проверяет отметку «сдано», условие для буфера, проверку работы и конспект к практике
/// </summary>
public class ReminderTests
{
    /// <summary>
    /// Отметка «сдано» ставится и снимается, не мешая архиву, а условие для буфера собирается из названия, срока и требований
    /// </summary>
    [Fact]
    public void A_manual_submit_mark_is_stored_and_the_condition_is_assembled()
    {
        var directory = Directory.CreateTempSubdirectory("steward-submit-");
        try
        {
            var store = new HomeworkStore(directory.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(store.ItemsPath("e1"))!);
            File.WriteAllText(store.ItemsPath("e1"), """[{"title":"Отчёт","due":"2026-11-02","requirements":"две страницы","activity":"99"}]""");
            Directory.CreateDirectory(store.AssignmentDirectory("99"));
            File.WriteAllText(Path.Combine(store.AssignmentDirectory("99"), "assignment.md"), "Условие из LMS");

            store.SetSubmitted("e1", 1, true);
            Assert.Equal([1], store.SubmittedNumbers("e1").ToArray());
            Assert.Empty(store.ArchivedNumbers("e1"));

            store.SetSubmitted("e1", 1, false);
            Assert.Empty(store.SubmittedNumbers("e1"));

            var condition = store.Condition("e1", 1);
            Assert.Contains("Отчёт", condition, StringComparison.Ordinal);
            Assert.Contains("Срок: 02.11.2026", condition, StringComparison.Ordinal);
            Assert.Contains("две страницы", condition, StringComparison.Ordinal);
            Assert.Contains("Условие из LMS", condition, StringComparison.Ordinal);
            Assert.Equal(string.Empty, store.Condition("e1", 2));
            Assert.Equal("99", store.ReadItems("e1")![0].ActivityId);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Строка задания передаёт нажатия «Открыть в LMS», «Отметить сданным» и «Скопировать условие», а сданное по данным LMS отметить заново нельзя
    /// </summary>
    [Fact]
    public void An_item_row_reports_the_new_buttons()
    {
        var calls = new List<string>();
        var open = new LessonItemViewModel(1, new HomeworkItem("А", null, string.Empty, string.Empty), false, _ => { }, _ => { }, _ => calls.Add("lms"), _ => calls.Add("submit"), _ => calls.Add("copy"));
        open.OpenInLmsCommand.Execute(null);
        open.ToggleSubmittedCommand.Execute(null);
        open.CopyConditionCommand.Execute(null);

        Assert.Equal(["lms", "submit", "copy"], calls);
        Assert.Equal("Отметить сданным", open.SubmitLabel);

        var marked = new LessonItemViewModel(1, new HomeworkItem("А", null, string.Empty, string.Empty, true, Submitted: true), false, _ => { }, _ => { });
        Assert.Equal("Вернуть в работу", marked.SubmitLabel);
        Assert.True(marked.CanToggleSubmitted);

        var lms = new LessonItemViewModel(1, new HomeworkItem("А", null, string.Empty, string.Empty, true), false, _ => { }, _ => { });
        Assert.False(lms.CanToggleSubmitted);

        var changed = new LessonItemViewModel(1, new HomeworkItem("А", null, string.Empty, string.Empty, Changed: DateOnly.FromDateTime(DateTime.Now)), false, _ => { }, _ => { });
        Assert.Contains("условие в LMS изменено", changed.Meta, StringComparison.Ordinal);
    }

    /// <summary>
    /// Просьба на проверку запрещает правки, называет разделы отчёта и все материалы, а конспект к практике собирает темы, формулировки и задачи
    /// </summary>
    [Fact]
    public void The_check_prompt_and_the_practice_brief_have_their_parts()
    {
        var lesson = new Lesson("e1", "Гейм-дизайн", new DateOnly(2026, 10, 19));
        var prompt = DraftCheckPrompt.Build(lesson, [@"D:\w\Задание 1.docx.txt"], [new HomeworkItem("Отчёт", null, "две страницы", string.Empty)], "t.txt", [@"D:\a\99\assignment.md"], new GuideMaterials([@"D:\g\guide.md"], [], []), "r.md");

        Assert.Contains("Ничего в работе не правь", prompt, StringComparison.Ordinal);
        Assert.Contains("## Соответствие условию", prompt, StringComparison.Ordinal);
        Assert.Contains("## Что нельзя подтвердить", prompt, StringComparison.Ordinal);
        Assert.Contains(@"D:\g\guide.md", prompt, StringComparison.Ordinal);
        Assert.Contains(@"D:\a\99\assignment.md", prompt, StringComparison.Ordinal);

        var reading = new TranscriptText("Баланс", string.Empty, [], ["Берём три предмета"], ["Сдать таблицу"], [], [new AnalysisTopic("Прогрессия", "Растёт по ступеням")], ["Баланс это согласованность"], true);
        var brief = PracticeBrief.Build("Гейм-дизайн", "19.10.2026", reading);

        Assert.Contains("Прогрессия: Растёт по ступеням", brief, StringComparison.Ordinal);
        Assert.Contains("Баланс это согласованность", brief, StringComparison.Ordinal);
        Assert.Contains("Сдать таблицу", brief, StringComparison.Ordinal);
        Assert.Equal(string.Empty, PracticeBrief.Build("П", "д", TranscriptText.Empty));
    }
}
