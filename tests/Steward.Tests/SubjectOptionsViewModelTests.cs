using Steward.Homework.Models;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Настройки предмета на вкладке «Домашние задания»: значения из хранилища, запись в фоне и откат при неудаче
/// </summary>
public class SubjectOptionsViewModelTests
{
    /// <summary>
    /// Список занятий создаёт предмету настройки из хранилища, а переключатель и выбор источника записывают новое значение в фоне, не трогая второе
    /// </summary>
    /// <returns>Задача теста</returns>
    [Fact]
    public async Task The_subject_gets_options_from_the_store_and_changes_are_saved_in_the_background()
    {
        var saved = new List<(string Subject, SubjectOptions Options)>();
        var written = new TaskCompletionSource();
        var model = new HomeworkFormatsViewModel([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word)], (_, _) => { }, _ => true, (_, _) => Task.FromResult((1, string.Empty)))
        {
            SubjectOptionsReader = _ => new SubjectOptions(true, TaskSourceEnum.Transcript),
            SubjectOptionsSaver = (subject, options) =>
            {
                saved.Add((subject, options));
                written.TrySetResult();
            },
        };
        model.Update([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word)]);

        var options = model.Subjects.Single().Options!;

        Assert.True(options.AutoPrepare);
        Assert.Equal(TaskSourceEnum.Transcript, options.SelectedSource.Source);
        Assert.False(string.IsNullOrEmpty(options.SourceHint));

        options.SelectedSource = options.Sources.Single(source => source.Source == TaskSourceEnum.Lms);
        await written.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(("Гейм-дизайн", new SubjectOptions(true, TaskSourceEnum.Lms)), saved.Single());
        Assert.Equal(TaskSourceEnum.Lms, options.SelectedSource.Source);
    }

    /// <summary>
    /// Неудачная запись возвращает прежнее значение и показывает причину, а следующая удачная причину убирает
    /// </summary>
    /// <returns>Задача теста</returns>
    [Fact]
    public async Task A_failed_save_reverts_the_value_and_shows_the_reason()
    {
        var fail = true;
        var model = new HomeworkFormatsViewModel([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word)], (_, _) => { }, _ => true, (_, _) => Task.FromResult((1, string.Empty)))
        {
            SubjectOptionsSaver = (_, _) =>
            {
                if (fail)
                    throw new IOException("файл занят");
            },
        };
        model.Update([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word)]);
        var options = model.Subjects.Single().Options!;

        options.AutoPrepare = true;
        await WaitAsync(() => options.HasError);

        Assert.False(options.AutoPrepare);
        Assert.Contains("файл занят", options.Error, StringComparison.Ordinal);

        fail = false;
        options.AutoPrepare = true;
        await WaitAsync(() => !options.HasError);

        Assert.True(options.AutoPrepare);
    }

    /// <summary>
    /// Ждёт, пока условие выполнится, но не дольше пяти секунд
    /// </summary>
    /// <param name="condition">Что ждём</param>
    /// <returns>Задача ожидания</returns>
    private static async Task WaitAsync(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < until)
            await Task.Delay(20);
    }
}
