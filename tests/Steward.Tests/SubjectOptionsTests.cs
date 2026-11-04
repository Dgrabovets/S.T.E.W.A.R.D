using Steward.Homework.Models;
using Steward.Homework.Storage;

namespace Steward.Tests;

/// <summary>
/// Настройки предмета: автоподготовка выключена по умолчанию, источник заданий выбирается или определяется по LMS, а задания предметов с LMS строятся без агента
/// </summary>
public sealed class SubjectOptionsTests : IDisposable
{
    /// <summary>
    /// Предмет, у которого условие написано в LMS
    /// </summary>
    private const string WITH_TEXT = "Философия";

    /// <summary>
    /// Предмет, у которого в LMS условия нет
    /// </summary>
    private const string WITHOUT_TEXT = "Гейм-дизайн";

    /// <summary>
    /// Папка теста
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "steward-subject-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>
    /// Хранилище в папке теста
    /// </summary>
    private readonly HomeworkStore _store;

    /// <summary>
    /// Создаёт хранилище и записывает работы LMS: у «Философии» условие есть, у «Гейм-дизайна» нет, плюс сданная работа и тест
    /// </summary>
    public SubjectOptionsTests()
    {
        _store = new HomeworkStore(_root);
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "activities.json"), """
            [
              {"subject":"Философия","name":"Задание 1","kind":0,"done":false,"id":"11"},
              {"subject":"Философия","name":"Задание 2","kind":0,"done":false,"id":"12","due":"2026-11-10"},
              {"subject":"Философия","name":"Старое","kind":0,"done":true,"id":"13"},
              {"subject":"Философия","name":"Тест","kind":1,"done":false,"id":"14"},
              {"subject":"Гейм-дизайн","name":"Задание 1","kind":0,"done":false,"id":"21"}
            ]
            """);
        Describe("11", "Разберите три основных направления западной философии и сравните их подходы к познанию.");
        Describe("12", "Подготовьте реферат по выбранной теме и приложите список литературы.");
        Describe("21", "Текст условия на странице не указан, смотри прикреплённые файлы.");
    }

    /// <summary>
    /// Удаляет папку теста
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Записывает описание работы LMS в том виде, в каком его пишет выгрузка
    /// </summary>
    /// <param name="id">Номер работы</param>
    /// <param name="condition">Текст раздела «Условие»</param>
    private void Describe(string id, string condition)
    {
        Directory.CreateDirectory(_store.AssignmentDirectory(id));
        File.WriteAllText(Path.Combine(_store.AssignmentDirectory(id), "assignment.md"), $"# Работа {id}\n\nПредмет: x\nСтраница в LMS: https://lms.example/mod/assign/view.php?id={id}\n\n## Условие\n\n{condition}\n\n## Прикреплённые файлы\n\n- a.pdf: D:\\a.pdf\n");
    }

    /// <summary>
    /// Записывает занятие предмета с расшифровкой и отметкой «расшифровывать»
    /// </summary>
    /// <param name="id">Ключ занятия</param>
    /// <param name="subject">Предмет</param>
    /// <param name="date">Дата</param>
    /// <returns>Занятие</returns>
    private Lesson AddLesson(string id, string subject, DateOnly date)
    {
        var lesson = new Lesson(id, subject, date);
        _store.SaveLessons([lesson]);
        _store.SetSubjectState(subject, LessonStateEnum.Fetch);
        Directory.CreateDirectory(Path.GetDirectoryName(_store.TranscriptPath(id))!);
        File.WriteAllText(_store.TranscriptPath(id), "== Расшифровка ==\n[00:00] Преподаватель: Начинаем.");

        return lesson;
    }

    /// <summary>
    /// Автоподготовка выключена по умолчанию, включается и выключается у одного предмета, не задевая остальные
    /// </summary>
    [Fact]
    public void Auto_prepare_is_off_by_default_and_changes_one_subject_only()
    {
        Assert.False(_store.SubjectOptionsFor(WITH_TEXT).AutoPrepare);

        _store.SetSubjectAutoPrepare(WITH_TEXT, true);

        Assert.Equal(new SubjectOptions(true), _store.SubjectOptionsFor(WITH_TEXT));
        Assert.Equal(new SubjectOptions(), _store.SubjectOptionsFor(WITHOUT_TEXT));

        _store.SetSubjectAutoPrepare(WITH_TEXT, false);

        Assert.Equal(new SubjectOptions(false), _store.SubjectOptionsFor(WITH_TEXT));
    }

    /// <summary>
    /// Несданные работы LMS становятся заданиями самого свежего занятия с расшифровкой, сданные работы и тесты пропускаются, повторный вызов ничего не дублирует, а номера прежних заданий не сдвигаются
    /// </summary>
    [Fact]
    public void Lms_works_become_items_of_the_newest_lesson_once_and_keep_old_numbers()
    {
        var old = AddLesson("p0", WITH_TEXT, new DateOnly(2026, 9, 1));
        var newest = AddLesson("p1", WITH_TEXT, new DateOnly(2026, 10, 20));
        var lessons = _store.LoadLessons();

        Assert.Equal(2, _store.SyncLmsItems(lessons));

        var items = _store.ReadItems(newest.Id)!;
        Assert.Equal(["Задание 1", "Задание 2"], items.Select(item => item.Title).ToArray());
        Assert.Equal(["11", "12"], items.Select(item => item.ActivityId).ToArray());
        Assert.Equal(new DateOnly(2026, 11, 10), items[1].Due);
        Assert.Equal("https://lms.example/mod/assign/view.php?id=12", items[1].Link);
        Assert.Null(_store.ReadItems(old.Id));
        Assert.Equal(0, _store.SyncLmsItems(lessons));
        Assert.Equal(2, _store.ReadItems(newest.Id)!.Count);

        File.WriteAllText(Path.Combine(_root, "activities.json"), File.ReadAllText(Path.Combine(_root, "activities.json")).Replace("""{"subject":"Философия","name":"Старое","kind":0,"done":true,"id":"13"}""", """{"subject":"Философия","name":"Новое","kind":0,"done":false,"id":"15"}"""));
        Describe("15", "Напишите эссе о роли разума в философии Нового времени и приведите примеры.");

        Assert.Equal(1, _store.SyncLmsItems(lessons));
        Assert.Equal(["11", "12", "15"], _store.ReadItems(newest.Id)!.Select(item => item.ActivityId).ToArray());
    }

    /// <summary>
    /// Задания LMS кладутся и к занятию без расшифровки: плашка занятия создаётся сразу, а расшифровка подтянется в неё, когда появится
    /// </summary>
    [Fact]
    public void Lms_works_land_on_a_lesson_that_has_no_transcript_yet()
    {
        var noTranscript = new Lesson("p9", WITH_TEXT, new DateOnly(2026, 10, 21));
        _store.SaveLessons([noTranscript]);
        _store.SetSubjectState(WITH_TEXT, LessonStateEnum.Fetch);

        Assert.Equal(2, _store.SyncLmsItems(_store.LoadLessons()));
        Assert.Equal(["Задание 1", "Задание 2"], _store.ReadItems("p9")!.Select(item => item.Title).ToArray());
    }

    /// <summary>
    /// Работа без написанного условия и без приложенного файла в список не попадает, а предмет без отметки «Расшифровывать» не получает ничего
    /// </summary>
    [Fact]
    public void Works_without_an_explicit_condition_and_undecided_subjects_get_nothing()
    {
        AddLesson("g1", WITHOUT_TEXT, new DateOnly(2026, 10, 19));
        Assert.Equal(0, _store.SyncLmsItems(_store.LoadLessons()));
        Assert.Null(_store.ReadItems("g1"));

        var undecided = new Lesson("u1", "Другой предмет", new DateOnly(2026, 10, 22));
        _store.SaveLessons([undecided]);
        Assert.Equal(0, _store.SyncLmsItems(_store.LoadLessons()));
        Assert.Null(_store.ReadItems("u1"));
    }

    /// <summary>
    /// Приложенный файл сам по себе считается формулировкой: условие копируется без агента, даже если текста на странице нет
    /// </summary>
    [Fact]
    public void An_attached_file_alone_counts_as_an_explicit_condition()
    {
        AddLesson("g1", WITHOUT_TEXT, new DateOnly(2026, 10, 19));
        File.WriteAllText(Path.Combine(_store.AssignmentDirectory("21"), "Условие.xlsx"), "данные");

        Assert.Equal(1, _store.SyncLmsItems(_store.LoadLessons()));
        Assert.Equal(["21"], _store.ReadItems("g1")!.Select(item => item.ActivityId).ToArray());
    }

    /// <summary>
    /// Отметка «Задания из LMS»: расшифровку не забирают и занятие не разбирают, а задания из LMS копируются и готовятся
    /// </summary>
    [Fact]
    public void The_lms_only_state_copies_lms_works_but_never_fetches_or_analyzes()
    {
        var lesson = new Lesson("p9", WITH_TEXT, new DateOnly(2026, 10, 21), RecordUrl: "https://record.example/9");
        _store.SaveLessons([lesson]);
        _store.SetSubjectState(WITH_TEXT, LessonStateEnum.LmsOnly);

        Assert.False(_store.ShouldFetch(lesson));
        Assert.False(_store.ShouldAnalyze(lesson));
        Assert.True(_store.PreparesHomework(lesson));
        Assert.Equal(2, _store.SyncLmsItems(_store.LoadLessons()));
        Assert.Equal(["Задание 1", "Задание 2"], _store.ReadItems("p9")!.Select(item => item.Title).ToArray());
    }

    /// <summary>
    /// Предмет с отметкой «Завершён» работ из LMS не получает
    /// </summary>
    [Fact]
    public void A_finished_subject_gets_no_lms_items()
    {
        _store.SaveLessons([new Lesson("p9", WITH_TEXT, new DateOnly(2026, 10, 21))]);
        _store.SetSubjectState(WITH_TEXT, LessonStateEnum.Done);

        Assert.Equal(0, _store.SyncLmsItems(_store.LoadLessons()));
    }

    /// <summary>
    /// Ключи задач Singularity по одному заданию: из списка заданий и из отрывка расшифровки видят друг друга, чтобы две кнопки не создали две задачи
    /// </summary>
    [Fact]
    public void Singularity_keys_link_an_item_with_the_same_excerpt_task()
    {
        var lesson = new Lesson("p9", WITH_TEXT, new DateOnly(2026, 10, 21));
        _store.SaveLessons([lesson]);
        Directory.CreateDirectory(Path.GetDirectoryName(_store.AnalysisPath("p9"))!);
        File.WriteAllText(_store.AnalysisPath("p9"), """{"topic":"Т","tasks":[{"title":"Другое","quote":"слова"},{"title":"Отчёт","quote":"сделайте отчёт"}]}""");
        Assert.True(_store.AddItem("p9", "Отчёт", "сделайте отчёт"));

        Assert.Equal(["hw:p9:1", "hwq:p9:2"], _store.SingularityKeysForItem(lesson, 1));
        Assert.Equal(["hwq:p9:2", "hw:p9:1"], _store.SingularityKeysForQuote(lesson, 2));
        Assert.Equal(["hwq:p9:1"], _store.SingularityKeysForQuote(lesson, 1));
    }

    /// <summary>
    /// Собственная отметка занятия «Задания из LMS» не даёт забрать расшифровку, даже если у предмета стоит «Расшифровывать»
    /// </summary>
    [Fact]
    public void A_lesson_marked_lms_only_is_not_fetched_under_a_fetching_subject()
    {
        var lesson = new Lesson("p9", WITH_TEXT, new DateOnly(2026, 10, 21), RecordUrl: "https://record.example/9");
        _store.SaveLessons([lesson]);
        _store.SetSubjectState(WITH_TEXT, LessonStateEnum.Fetch);
        Assert.True(_store.ShouldFetch(lesson));

        _store.SetState("p9", LessonStateEnum.LmsOnly);

        Assert.False(_store.ShouldFetch(lesson));
    }
}



