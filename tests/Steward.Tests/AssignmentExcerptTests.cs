using Steward.Homework.Models;
using Steward.Homework.Storage;
using Steward.Models;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Задания с занятия: слова преподавателя сохраняются вместе с репликами до и после, показываются на вкладке «Задания», а кнопка «Добавить в задание» заносит задание в список только по нажатию
/// </summary>
public class AssignmentExcerptTests
{
    /// <summary>
    /// Расшифровка из восьми реплик, задание сказано в пятой
    /// </summary>
    private const string TRANSCRIPT = "== Резюме ==\nТема встречи: Баланс\n\n== Расшифровка ==\n[00:01] Преподаватель: Раз.\n[00:02] Преподаватель: Два.\n[00:03] Преподаватель: Три.\n[00:04] Преподаватель: Четыре.\n[00:05] Преподаватель: Сделайте отчёт по игре до пятницы.\n[00:06] Студент: Какой объём?\n[00:07] Преподаватель: Две страницы.\n[00:08] Преподаватель: Шесть.\n[00:09] Преподаватель: Семь.\n[00:10] Преподаватель: Восемь.";

    /// <summary>
    /// Разбор с одним заданием, у которого есть цитата
    /// </summary>
    /// <param name="quote">Слова преподавателя</param>
    /// <returns>Разбор</returns>
    private static LessonAnalysis Analysis(string quote) => new("Баланс", string.Empty, [], [], [], [], [new AnalysisTask("Отчёт по игре", "пятница", quote)]);

    /// <summary>
    /// К заданию дописываются по три реплики до и после слов преподавателя, и они дословно из расшифровки, а не из ответа агента
    /// </summary>
    [Fact]
    public void Excerpts_hold_three_lines_around_the_quote_taken_from_the_transcript()
    {
        var task = Analysis("сделайте отчёт по игре до пятницы").WithExcerpts(TRANSCRIPT).Tasks.Single();

        Assert.Equal(["[00:02] Преподаватель: Два.", "[00:03] Преподаватель: Три.", "[00:04] Преподаватель: Четыре."], task.Before);
        Assert.Equal(["[00:05] Преподаватель: Сделайте отчёт по игре до пятницы."], task.Said);
        Assert.Equal(["[00:06] Студент: Какой объём?", "[00:07] Преподаватель: Две страницы.", "[00:08] Преподаватель: Шесть."], task.After);
    }

    /// <summary>
    /// Цитата, которой нет в расшифровке, не даёт выдуманных строк: остаётся только то, что написал агент
    /// </summary>
    [Fact]
    public void A_quote_missing_from_the_transcript_leaves_no_lines()
    {
        var task = Analysis("совсем другие слова").WithExcerpts(TRANSCRIPT).Tasks.Single();

        Assert.False(task.HasExcerpt);
        Assert.Equal("совсем другие слова", task.Quote);
    }

    /// <summary>
    /// Отрывок переживает запись в файл и чтение обратно
    /// </summary>
    [Fact]
    public void Excerpts_survive_a_round_trip_through_the_file()
    {
        var written = Analysis("сделайте отчёт по игре до пятницы").WithExcerpts(TRANSCRIPT);

        var read = LessonAnalysis.Parse(written.ToJson())!.Tasks.Single();

        Assert.Equal(written.Tasks.Single().Said, read.Said);
        Assert.Equal(3, read.Before!.Count);
    }

    /// <summary>
    /// Вкладка «Задания» есть, если на занятии были задания, в том числе у старых разборов без цитат (тогда с пометкой); для занятия без заданий её нет
    /// </summary>
    [Fact]
    public void The_assignments_tab_appears_when_the_lesson_had_tasks_and_not_otherwise()
    {
        var withQuote = Open(Analysis("сделайте отчёт по игре до пятницы").WithExcerpts(TRANSCRIPT), out _);
        var oldAnalysis = Open(Analysis(string.Empty), out _);
        var none = Open(new LessonAnalysis("Баланс", "Обзор", ["Пройдено"], [], [], [], []), out _);

        Assert.Contains("Задания", withQuote.ReaderTabs.Select(tab => tab.Title));
        Assert.Contains("Задания", oldAnalysis.ReaderTabs.Select(tab => tab.Title));
        Assert.DoesNotContain("Задания", none.ReaderTabs.Select(tab => tab.Title));
        Assert.Single(withQuote.Assignments);
        Assert.Equal("Задание 1", withQuote.Assignments[0].Heading);
        Assert.True(oldAnalysis.Assignments[0].HasWarning);
    }

    /// <summary>
    /// Кнопка «Только в Singularity» заводит задачу по номеру задания в разборе и не трогает список заданий
    /// </summary>
    [Fact]
    public async Task The_singularity_button_creates_only_a_task_and_never_touches_the_list()
    {
        var created = new List<(string Lesson, int Index)>();
        var added = new List<string>();
        var model = Open(Analysis("сделайте отчёт по игре до пятницы").WithExcerpts(TRANSCRIPT), out _, (id, title, _, _) =>
        {
            added.Add(title);

            return true;
        });
        model.AssignmentToSingularity = (id, index) =>
        {
            created.Add((id, index));

            return Task.FromResult(("Задача заведена в Singularity: Отчёт по игре", false));
        };
        model.Lessons.Single().OpenCommand.Execute(null);
        var row = model.Assignments.Single();

        row.SingularityCommand.Execute(null);
        await Task.Delay(300);

        Assert.Equal([("a", 1)], created);
        Assert.Empty(added);
        Assert.Equal("Заведено в Singularity", row.SingularityText);
        Assert.False(row.SingularityCommand.CanExecute(null));
        Assert.True(row.AddCommand.CanExecute(null));
    }

    /// <summary>
    /// Задача для Singularity собирается из отрывка: слова преподавателя в заметке, срок из разбора становится датой начала
    /// </summary>
    [Fact]
    public void The_singularity_task_carries_the_excerpt_and_the_due_date()
    {
        var lesson = new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 25));
        var task = Analysis("сделайте отчёт по игре до пятницы").WithExcerpts(TRANSCRIPT).Tasks.Single() with { Due = "02.11" };

        var request = Steward.Homework.Services.HomeworkTaskBuilder.BuildFromQuote(lesson, task, 1, null, TimeSpan.FromHours(3));

        Assert.Equal("ДЗ: Отчёт по игре", request.Title);
        Assert.Equal("hwq:a:1", request.ExternalId);
        Assert.StartsWith("2026-11-02T00:00:00", request.Start);
        Assert.Contains("Сделайте отчёт по игре до пятницы.", request.Note);
    }

    /// <summary>
    /// Задание попадает в список только по нажатию кнопки; повторное нажатие невозможно
    /// </summary>
    [Fact]
    public async Task The_button_adds_the_assignment_once_and_only_when_pressed()
    {
        var calls = new List<(string Lesson, string Title, string Quote)>();
        var model = Open(Analysis("сделайте отчёт по игре до пятницы").WithExcerpts(TRANSCRIPT), out var lesson, (id, title, quote, _) =>
        {
            calls.Add((id, title, quote));

            return true;
        });
        var row = model.Assignments.Single();

        Assert.Empty(calls);
        Assert.Equal("Добавить в задание", row.ButtonText);

        row.AddCommand.Execute(null);
        await Task.Delay(300);

        Assert.Equal([("a", "Отчёт по игре", "сделайте отчёт по игре до пятницы")], calls);
        Assert.Equal("Уже в заданиях", row.ButtonText);
        Assert.False(row.AddCommand.CanExecute(null));
        Assert.NotNull(lesson);
    }

    /// <summary>
    /// Файл заданий дополняется в конец: прежние задания и их номера остаются, а такое же задание второй раз не добавляется
    /// </summary>
    [Fact]
    public void The_store_appends_to_the_end_and_skips_duplicates()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var store = new HomeworkStore(directory.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(store.ItemsPath("a"))!);
            File.WriteAllText(store.ItemsPath("a"), """[{"title":"Первое","due":"","requirements":"","link":"","done":false,"activity":"","evidence":"","confidence":"","doubt":""}]""");

            Assert.True(store.AddItem("a", "Второе", "слова преподавателя", "2026-11-02"));
            Assert.False(store.AddItem("a", "Второе", "слова преподавателя"));

            var items = store.ReadItems("a")!;
            Assert.Equal(["Первое", "Второе"], items.Select(item => item.Title).ToArray());
            Assert.Equal("слова преподавателя", items[1].Evidence);
            Assert.Equal(new DateOnly(2026, 11, 2), items[1].Due);
            Assert.Contains(2, store.ConfirmedNumbers("a"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Нечитаемый файл заданий не затирается добавлением
    /// </summary>
    [Fact]
    public void A_broken_items_file_is_never_overwritten()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var store = new HomeworkStore(directory.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(store.ItemsPath("a"))!);
            File.WriteAllText(store.ItemsPath("a"), "не json");

            Assert.Throws<InvalidDataException>(() => store.AddItem("a", "Новое", "слова"));
            Assert.Equal("не json", File.ReadAllText(store.ItemsPath("a")));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Занятие без расшифровки, но с заданием из LMS, показывается на вкладке «Домашние задания» и готовится по кнопке; занятие без расшифровки и без заданий не показывается
    /// </summary>
    [Fact]
    public async Task A_lesson_without_a_transcript_is_listed_when_it_holds_an_lms_item()
    {
        var model = new HomeworkFormatsViewModel(
            [(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word), (new Lesson("b", "Гейм-дизайн", new DateOnly(2026, 10, 12)), HomeworkFormatEnum.Word)],
            (_, _) => { },
            _ => false,
            (_, _) => Task.FromResult((1, string.Empty)))
        {
            ItemsReader = id => id == "a" ? [(1, new HomeworkItem("Работа из LMS", null, string.Empty, string.Empty, ActivityId: "11"), false)] : null,
        };
        model.Update([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word), (new Lesson("b", "Гейм-дизайн", new DateOnly(2026, 10, 12)), HomeworkFormatEnum.Word)]);
        await model.WhenPipelineLoaded();
        var subject = model.Subjects.Single();

        Assert.Equal(["a"], subject.HomeworkLessons.Select(lesson => lesson.Id).ToArray());
        Assert.True(subject.HomeworkLessons[0].CanPrepare);
    }

    /// <summary>
    /// Собирает список с одним занятием, у которого открыта читалка
    /// </summary>
    /// <param name="analysis">Разбор занятия</param>
    /// <param name="lesson">Занятие в списке</param>
    /// <param name="adder">Добавление задания в список; по умолчанию всегда успешное</param>
    /// <returns>Список занятий с открытой читалкой</returns>
    private static HomeworkFormatsViewModel Open(LessonAnalysis analysis, out LessonFormatViewModel lesson, Func<string, string, string, string, bool>? adder = null)
    {
        var model = new HomeworkFormatsViewModel([(new Lesson("a", "Гейм-дизайн", new DateOnly(2026, 10, 19)), HomeworkFormatEnum.Word)], (_, _) => { }, _ => true, (_, _) => Task.FromResult((1, string.Empty)))
        {
            TranscriptReader = _ => TRANSCRIPT,
            AnalysisReader = _ => analysis,
            AssignmentAdder = adder ?? ((_, _, _, _) => true),
        };
        lesson = model.Lessons.Single();
        lesson.OpenCommand.Execute(null);

        return model;
    }

    /// <summary>
    /// Пустой массив в любой записи не мешает добавлению, а срок словами остаётся в требованиях, а не пропадает
    /// </summary>
    [Fact]
    public void A_formatted_empty_array_accepts_an_item_and_a_due_in_words_is_kept()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var store = new HomeworkStore(directory.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(store.ItemsPath("a"))!);
            File.WriteAllText(store.ItemsPath("a"), "[\n  \n]");

            Assert.True(store.AddItem("a", "Отчёт", "слова", "к следующему занятию"));

            var item = store.ReadItems("a")!.Single();
            Assert.Null(item.Due);
            Assert.Equal("Срок: к следующему занятию", item.Requirements);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Одно правило «то же задание»: пробелы по краям и регистр не мешают, пустые название и цитата ничему не равны
    /// </summary>
    [Fact]
    public void The_same_task_rule_ignores_case_and_edge_spaces_and_empty_values()
    {
        Assert.True(Steward.Homework.Services.TaskMatch.Same(" Отчёт ", string.Empty, "отчёт", string.Empty));
        Assert.True(Steward.Homework.Services.TaskMatch.Same("А", " слова ", "Б", "СЛОВА"));
        Assert.False(Steward.Homework.Services.TaskMatch.Same(string.Empty, string.Empty, string.Empty, string.Empty));
    }
}


