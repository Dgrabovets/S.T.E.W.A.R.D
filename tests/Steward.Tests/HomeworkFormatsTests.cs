using Steward.Homework.Models;
using Steward.Hosting;
using Steward.Models;
using Steward.Homework.Storage;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Проверяет хранение форматов ДЗ и окно, где их выбирают
/// </summary>
public class HomeworkFormatsTests
{
    /// <summary>
    /// Без выбора берётся Word, выбранный формат запоминается и переживает новую загрузку
    /// </summary>
    [Fact]
    public void A_chosen_format_is_remembered_and_the_default_is_word()
    {
        var directory = Directory.CreateTempSubdirectory("steward-homework-");
        try
        {
            var store = new HomeworkStore(directory.FullName);
            Assert.Equal(HomeworkFormatEnum.Word, store.FormatFor("db-1"));

            store.SetFormat("db-1", HomeworkFormatEnum.Presentation);

            Assert.Equal(HomeworkFormatEnum.Presentation, new HomeworkStore(directory.FullName).FormatFor("db-1"));
            Assert.Equal(HomeworkFormatEnum.Word, store.FormatFor("other"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Занятия читаются от свежих к старым, испорченный файл даёт пустой список
    /// </summary>
    [Fact]
    public void Lessons_are_newest_first_and_a_broken_file_is_empty()
    {
        var directory = Directory.CreateTempSubdirectory("steward-homework-");
        try
        {
            var store = new HomeworkStore(directory.FullName);
            Assert.Empty(store.LoadLessons());

            File.WriteAllText(Path.Combine(directory.FullName, "lessons.json"), """[{"id":"a","title":"А","date":"2026-10-20"},{"id":"b","title":"Б","date":"2026-10-25"}]""");
            Assert.Equal(["b", "a"], store.LoadLessons().Select(lesson => lesson.Id).ToArray());

            File.WriteAllText(Path.Combine(directory.FullName, "lessons.json"), "{ не json");
            Assert.Empty(store.LoadLessons());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Выбор формата в строке переключает отметку и сообщает о выборе
    /// </summary>
    [Fact]
    public void Picking_a_format_moves_the_selection_and_reports_it()
    {
        var picked = new List<(string, HomeworkFormatEnum)>();
        var model = new HomeworkFormatsViewModel([(new Lesson("a", "А", new DateOnly(2026, 10, 25)), HomeworkFormatEnum.Word)], (id, format) => picked.Add((id, format)), _ => true, _ => Task.FromResult((0, string.Empty)));
        var options = model.Lessons.Single().Options;

        options.Single(option => option.Format == HomeworkFormatEnum.Code).SelectCommand.Execute(null);

        Assert.Equal([("a", HomeworkFormatEnum.Code)], picked);
        Assert.Equal([HomeworkFormatEnum.Code], options.Where(option => option.IsSelected).Select(option => option.Format).ToArray());
        Assert.False(model.IsEmpty);
    }

    /// <summary>
    /// Кнопка «Подготовить» видна только у занятий с расшифровкой и передаёт нужное занятие
    /// </summary>
    [Fact]
    public void Prepare_is_offered_only_with_a_transcript_and_passes_the_lesson()
    {
        var prepared = new List<string>();
        var withText = new Lesson("a", "А", new DateOnly(2026, 10, 25));
        var without = new Lesson("b", "Б", new DateOnly(2026, 10, 24));
        var model = new HomeworkFormatsViewModel([(withText, HomeworkFormatEnum.Word), (without, HomeworkFormatEnum.Word)], (_, _) => { }, lesson => lesson.Id == "a", lesson =>
        {
            prepared.Add(lesson.Id);

            return Task.FromResult((2, string.Empty));
        });

        model.Lessons[0].PrepareCommand.Execute(null);

        Assert.True(model.Lessons[0].HasTranscript);
        Assert.False(model.Lessons[1].HasTranscript);
        Assert.Equal(["a"], prepared);
        Assert.Equal(ActivityStateEnum.Done, model.Lessons[0].Activity);
        Assert.Equal("Найдено заданий: 2", model.Lessons[0].Status);
    }

    /// <summary>
    /// Ошибка подготовки показывается текстом, а галочка не рисуется
    /// </summary>
    [Fact]
    public void A_failed_preparation_shows_the_error_and_no_tick()
    {
        var model = new HomeworkFormatsViewModel([(new Lesson("a", "А", new DateOnly(2026, 10, 25)), HomeworkFormatEnum.Word)], (_, _) => { }, _ => true, _ => Task.FromResult((0, "Claude не ответил")));

        model.Lessons[0].PrepareCommand.Execute(null);

        Assert.True(model.Lessons[0].Failed);
        Assert.Equal(ActivityStateEnum.None, model.Lessons[0].Activity);
        Assert.Equal("Claude не ответил", model.Lessons[0].Status);
    }

    /// <summary>
    /// Запуск Claude идёт без сохранения сессии, только с чтением и записью файлов и только в рабочей папке
    /// </summary>
    [Fact]
    public void The_background_run_keeps_no_chat_and_limits_the_tools()
    {
        var arguments = ClaudeAgent.Arguments("просьба", @"D:\data\homework");

        Assert.Contains("--no-session-persistence", arguments);
        Assert.Equal("Read,Write", arguments[arguments.ToList().IndexOf("--tools") + 1]);
        Assert.Equal(@"D:\data\homework", arguments[arguments.ToList().IndexOf("--add-dir") + 1]);
        Assert.Equal("просьба", arguments[arguments.ToList().IndexOf("-p") + 1]);
    }
}