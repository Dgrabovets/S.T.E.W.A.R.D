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
    /// Автоподготовка выключена по умолчанию, включается и выключается у одного предмета, не задевая остальные и источник заданий
    /// </summary>
    [Fact]
    public void Auto_prepare_is_off_by_default_and_changes_one_subject_only()
    {
        Assert.False(_store.SubjectOptionsFor(WITH_TEXT).AutoPrepare);
        Assert.Equal(TaskSourceEnum.Auto, _store.SubjectOptionsFor(WITH_TEXT).TaskSource);

        _store.SetSubjectAutoPrepare(WITH_TEXT, true);
        _store.SetSubjectTaskSource(WITH_TEXT, TaskSourceEnum.Lms);

        Assert.Equal(new SubjectOptions(true, TaskSourceEnum.Lms), _store.SubjectOptionsFor(WITH_TEXT));
        Assert.Equal(new SubjectOptions(), _store.SubjectOptionsFor(WITHOUT_TEXT));

        _store.SetSubjectAutoPrepare(WITH_TEXT, false);

        Assert.Equal(new SubjectOptions(false, TaskSourceEnum.Lms), _store.SubjectOptionsFor(WITH_TEXT));
    }

    /// <summary>
    /// «Определять самому»: условие в LMS есть, значит задания там; одна заглушка вместо текста или сданные работы, значит задания на занятии; явный выбор сильнее
    /// </summary>
    [Fact]
    public void The_automatic_source_follows_the_condition_text_in_lms_and_an_explicit_choice_wins()
    {
        Assert.Equal(TaskSourceEnum.Lms, _store.TaskSourceFor(WITH_TEXT));
        Assert.Equal(TaskSourceEnum.Transcript, _store.TaskSourceFor(WITHOUT_TEXT));
        Assert.Equal(TaskSourceEnum.Transcript, _store.TaskSourceFor("Неизвестный предмет"));

        _store.SetSubjectTaskSource(WITHOUT_TEXT, TaskSourceEnum.Lms);
        _store.SetSubjectTaskSource(WITH_TEXT, TaskSourceEnum.Transcript);

        Assert.Equal(TaskSourceEnum.Lms, _store.TaskSourceFor(WITHOUT_TEXT));
        Assert.Equal(TaskSourceEnum.Transcript, _store.TaskSourceFor(WITH_TEXT));
    }

    /// <summary>
    /// Поиск заданий агентом не запускается для предмета с заданиями в LMS, но запускается для остальных
    /// </summary>
    [Fact]
    public void The_agent_search_is_skipped_for_a_subject_whose_tasks_are_in_lms()
    {
        var philosophy = AddLesson("p1", WITH_TEXT, new DateOnly(2026, 10, 20));
        var design = AddLesson("g1", WITHOUT_TEXT, new DateOnly(2026, 10, 19));

        Assert.False(_store.ShouldExtract(philosophy));
        Assert.True(_store.ShouldExtract(design));
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
    /// Предмет с заданиями на занятии и предмет без расшифровки карточек из LMS не получают
    /// </summary>
    [Fact]
    public void Subjects_with_lessons_tasks_or_without_a_transcript_get_no_lms_items()
    {
        AddLesson("g1", WITHOUT_TEXT, new DateOnly(2026, 10, 19));
        var noTranscript = new Lesson("p9", WITH_TEXT, new DateOnly(2026, 10, 21));
        _store.SaveLessons([noTranscript]);
        _store.SetSubjectState(WITH_TEXT, LessonStateEnum.Fetch);

        Assert.Equal(0, _store.SyncLmsItems(_store.LoadLessons()));
        Assert.Null(_store.ReadItems("g1"));
        Assert.Null(_store.ReadItems("p9"));
    }
}
