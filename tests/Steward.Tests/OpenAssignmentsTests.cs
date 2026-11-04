using Steward.Homework.Models;
using Steward.Models;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Вкладка расшифровок: читалка не остаётся от прежнего предмета, прошедшие занятия приглушены и подписаны возрастом, неразобранные задания светятся плашкой, убранное задание возвращается кнопкой «Откатить»
/// </summary>
public class OpenAssignmentsTests
{
    /// <summary>
    /// Разбор с двумя заданиями
    /// </summary>
    private static readonly LessonAnalysis TwoTasks = new("Баланс", string.Empty, [], [], [], [], [new AnalysisTask("Отчёт", "пятница", "сделайте отчёт"), new AnalysisTask("Прототип", "среда", "покажите прототип")]);

    /// <summary>
    /// Собирает список из занятий разных предметов
    /// </summary>
    /// <param name="lessons">Занятия: ключ, предмет, день</param>
    /// <returns>Список без чтения файлов</returns>
    private static HomeworkFormatsViewModel Build(params (string Id, string Subject, DateOnly Date)[] lessons) => new(
        [.. lessons.Select(item => (new Lesson(item.Id, item.Subject, item.Date), HomeworkFormatEnum.Word))],
        (_, _) => { },
        _ => true,
        (_, _) => Task.FromResult((0, string.Empty)));

    /// <summary>
    /// Ждёт, пока условие выполнится: действия карточки идут в фоне
    /// </summary>
    /// <param name="condition">Условие</param>
    /// <returns>Задача ожидания</returns>
    private static async Task Until(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
            await Task.Delay(30);

        Assert.True(condition());
    }

    /// <summary>
    /// Выбор другого предмета закрывает читалку: расшифровка прежнего предмета не остаётся в окне предпросмотра
    /// </summary>
    [Fact]
    public void Picking_another_subject_closes_the_open_transcript()
    {
        var model = Build(("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), ("b", "Философия", new DateOnly(2026, 10, 20)));
        var game = model.Subjects.Single(subject => subject.Title == "Гейм-дизайн");
        var philosophy = model.Subjects.Single(subject => subject.Title == "Философия");
        game.SelectCommand.Execute(null);
        model.Lessons.Single(lesson => lesson.Id == "a").OpenCommand.Execute(null);
        Assert.True(model.HasOpenLesson);

        game.SelectCommand.Execute(null);
        Assert.True(model.HasOpenLesson);

        philosophy.SelectCommand.Execute(null);

        Assert.False(model.HasOpenLesson);
        Assert.All(model.Lessons, lesson => Assert.False(lesson.IsOpen));
    }

    /// <summary>
    /// Предмет с отметкой «Завершён» получает ленту на плитке, а счётчики остаются живыми и показывают сданное по данным LMS
    /// </summary>
    [Fact]
    public void A_completed_subject_gets_a_ribbon_and_keeps_live_counters()
    {
        var activities = new[] { new ActivityInfo("Философия", "Эссе", false, true), new ActivityInfo("Философия", "Реферат", false, false), new ActivityInfo("Философия", "Тест", true, true) };
        var subject = new SubjectViewModel("Философия", [], LessonStateEnum.Done, (_, _) => { });
        subject.SetActivities(activities);

        Assert.True(subject.IsCompleted);
        Assert.Equal("Сдано 1 из 2", subject.AssignmentsText);
        Assert.Equal(0.5, subject.AssignmentsProgress);
        Assert.Equal("Тесты 1 из 1", subject.QuizzesText);

        subject.SetActivities([.. activities.Select(item => item with { Done = true })]);

        Assert.Equal("Сдано 2 из 2", subject.AssignmentsText);
        Assert.False(new SubjectViewModel("Философия", [], LessonStateEnum.Fetch, (_, _) => { }).IsCompleted);
    }

    /// <summary>
    /// Свежее занятие предмета не выделено, прошедшие приглушены и подписаны возрастом
    /// </summary>
    [Fact]
    public void Only_the_newest_lesson_is_current_and_older_ones_carry_an_age()
    {
        var model = Build(("new", "Гейм-дизайн", new DateOnly(2026, 11, 2)), ("old", "Гейм-дизайн", new DateOnly(2026, 10, 19)), ("other", "Философия", new DateOnly(2026, 9, 1)));

        Assert.True(model.Lessons.Single(lesson => lesson.Id == "new").IsCurrent);
        Assert.Equal(string.Empty, model.Lessons.Single(lesson => lesson.Id == "new").Age);
        Assert.True(model.Lessons.Single(lesson => lesson.Id == "old").IsPast);
        Assert.NotEqual(string.Empty, model.Lessons.Single(lesson => lesson.Id == "old").Age);
        Assert.True(model.Lessons.Single(lesson => lesson.Id == "other").IsCurrent);
    }

    /// <summary>
    /// Возраст занятия подписан по-русски: сегодня, вчера, дни, недели, месяцы
    /// </summary>
    [Fact]
    public void Age_is_worded_in_days_weeks_and_months()
    {
        var today = new DateOnly(2026, 11, 3);

        Assert.Equal("сегодня", LessonFormatViewModel.AgeOf(today, today));
        Assert.Equal("вчера", LessonFormatViewModel.AgeOf(today.AddDays(-1), today));
        Assert.Equal("5 дн. назад", LessonFormatViewModel.AgeOf(today.AddDays(-5), today));
        Assert.Equal("3 нед. назад", LessonFormatViewModel.AgeOf(today.AddDays(-21), today));
        Assert.Equal("3 мес. назад", LessonFormatViewModel.AgeOf(today.AddDays(-95), today));
    }

    /// <summary>
    /// Задание без реакции владельца считается неразобранным и светится плашкой на занятии и плитке; добавленное, заведённое в Singularity и убранное не считаются
    /// </summary>
    [Fact]
    public async Task Only_assignments_without_a_reaction_count_as_open()
    {
        var model = Build(("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)));
        model.AnalysisReader = _ => TwoTasks;
        model.AssignmentStateReader = _ => [new AssignmentState(false, false, false), new AssignmentState(false, false, false)];
        model.Update([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word)]);
        await model.WhenPipelineLoaded();
        var lesson = model.Lessons.Single();

        Assert.Equal(2, lesson.OpenAssignments);
        Assert.Equal("Не разобрано: 2", lesson.OpenAssignmentsText);
        Assert.Equal(2, model.Subjects.Single().OpenAssignments);

        model.AssignmentStateReader = _ => [new AssignmentState(true, false, false), new AssignmentState(false, true, false)];
        model.Update([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word)]);
        await model.WhenPipelineLoaded();

        Assert.False(lesson.HasOpenAssignments);
        Assert.False(model.Subjects.Single().HasOpenAssignments);
    }

    /// <summary>
    /// Собирает список с открытой читалкой, у которого разбор из двух заданий и записывающая «Убрать» заглушка
    /// </summary>
    /// <param name="calls">Сюда пишутся вызовы записи: название и признак «убрать»</param>
    /// <returns>Список с открытым занятием</returns>
    private static HomeworkFormatsViewModel OpenWithDismisser(List<(string Title, bool Dismissed)> calls)
    {
        var dismissed = new HashSet<string>();
        var model = Build(("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)));
        model.AnalysisReader = _ => TwoTasks;
        model.TranscriptReader = _ => "== Расшифровка ==\n[00:01] Преподаватель: сделайте отчёт";
        model.AssignmentStateReader = _ => [.. TwoTasks.Tasks.Select(task => new AssignmentState(false, false, dismissed.Contains(task.Title)))];
        model.AssignmentDismisser = (_, title, value) =>
        {
            calls.Add((title, value));
            if (value)
                dismissed.Add(title);
        };
        model.Lessons.Single().OpenCommand.Execute(null);

        return model;
    }

    /// <summary>
    /// «Убрать» прячет задание сразу, но файл не трогает, пока окошко висит; «Откатить» возвращает задание, и в файл ничего не записывалось
    /// </summary>
    [Fact]
    public async Task Dismissing_hides_the_assignment_at_once_and_undo_restores_it_without_writing()
    {
        var calls = new List<(string Title, bool Dismissed)>();
        var model = OpenWithDismisser(calls);
        model.Toast.TickMilliseconds = 1000;
        Assert.Equal(2, model.Assignments.Count);

        model.Assignments[0].DismissCommand.Execute(null);
        await Until(() => model.Toast.IsVisible);

        Assert.Empty(calls);
        Assert.Equal(["Прототип"], model.Assignments.Select(row => row.Title).ToArray());
        Assert.True(model.Toast.HasUndo);
        Assert.Equal("Задание убрано", model.Toast.Text);

        model.Toast.UndoCommand.Execute(null);
        await Until(() => model.Assignments.Count == 2);

        Assert.Empty(calls);
        Assert.False(model.Toast.IsVisible);
        Assert.Equal(["Отчёт", "Прототип"], model.Assignments.Select(row => row.Title).ToArray());
    }

    /// <summary>
    /// Когда время окошка вышло, задание убирается окончательно: оно записано в файл и на вкладку не возвращается
    /// </summary>
    [Fact]
    public async Task A_dismissed_assignment_is_removed_for_good_when_the_toast_expires()
    {
        var calls = new List<(string Title, bool Dismissed)>();
        var model = OpenWithDismisser(calls);
        model.Toast.TickMilliseconds = 1;

        model.Assignments[0].DismissCommand.Execute(null);
        await Until(() => model.Toast.IsVisible);
        await Until(() => !model.Toast.IsVisible);
        await Until(() => calls.Count == 1);
        await Task.Delay(200);

        Assert.Equal([("Отчёт", true)], calls);
        Assert.Equal(["Прототип"], model.Assignments.Select(row => row.Title).ToArray());
        Assert.Equal(1, model.Lessons.Single().OpenAssignments);
    }

    /// <summary>
    /// Закрытие окошка и новое окошко тоже делают действие окончательным, а закрытие приложения не теряет его
    /// </summary>
    [Fact]
    public void Closing_replacing_or_flushing_the_toast_commits_the_action()
    {
        var committed = new List<string>();
        var toast = new ToastViewModel();

        toast.Show("Первое", () => { }, () => committed.Add("первое"));
        toast.CloseCommand.Execute(null);
        toast.Show("Второе", () => { }, () => committed.Add("второе"));
        toast.Show("Третье", () => { }, () => committed.Add("третье"));
        toast.Flush();
        toast.Show("Четвёртое", () => committed.Add("откат"), () => committed.Add("четвёртое"));
        toast.UndoCommand.Execute(null);

        Assert.Equal(["первое", "второе", "третье", "откат"], committed);
    }

    /// <summary>
    /// Закрытое окошко оставляет действие как есть, новое сообщение заменяет прежнее, а само окошко гаснет по таймеру
    /// </summary>
    [Fact]
    public async Task The_toast_closes_replaces_and_expires()
    {
        var toast = new ToastViewModel { TickMilliseconds = 1 };
        var undone = 0;

        toast.Show("Первое", () => undone++);
        toast.CloseCommand.Execute(null);

        Assert.False(toast.IsVisible);
        Assert.False(toast.UndoCommand.CanExecute(null));
        Assert.Equal(0, undone);

        toast.Show("Второе", () => undone++);
        toast.Show("Третье");

        Assert.Equal("Третье", toast.Text);
        Assert.False(toast.HasUndo);

        await Until(() => !toast.IsVisible);
        Assert.Equal(0, undone);

        await toast.ShowAsync("Четвёртое", () => undone++);
        Assert.False(toast.IsVisible);
        Assert.Equal(0, undone);
    }
}
