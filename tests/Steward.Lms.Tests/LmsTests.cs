using Steward.Homework.Models;
using Steward.Homework.Storage;
using Steward.Lms.Models;
using Steward.Lms.Services;
using Steward.Mail.Models;

namespace Steward.Lms.Tests;

/// <summary>
/// Проверяет разбор страницы занятий LMS, превращение вебинаров в занятия и текст файла расшифровки
/// </summary>
public class LmsTests
{
    /// <summary>
    /// Кусок страницы: текущий вебинар без записи и два прошедших, у одного из них в названии есть подпись «Лекция 1»
    /// </summary>
    private const string Page = """
        <h2>Текущие вебинары</h2>
        <ul><li class="moodle__content"><h3 class="moodle__title-2">LMS_P_Гейм-дизайн [1 семестр] [Медов А.А.]_26.10.2026_8</h3><a href="https://my.mts-link.ru/j/Ranepa/1/join">Присоединиться</a></li></ul>
        <h2>Прошедшие вебинары</h2>
        <ul data-pagination-content>
          <li class="moodle__content">
            <div class="moodle__titles">
              <h3 class="moodle__title-2">LMS_P_Гейм-дизайн [1 семестр] [Медов А.А., Чистова Е.В.] [ОМ-РКИ-26 (1 сем.)]_Лекция 1, 07.09.2026</h3>
              <p class="moodle__subtitle">07 сентября 2026, 19:25 (длительность 01:59)</p>
            </div><a class="moodle-button" href="https://my.mts-link.ru/j/Ranepa/24692920129/record-new/24116923549">Запись вебинара</a>
          </li>
          <li class="moodle__content">
            <h3 class="moodle__title-2">LMS_P_Гейм-дизайн [1 семестр] [Медов А.А., Чистова Е.В.] [ОМ-РКИ-26 (1 сем.)]_21.09.2026_3</h3>
            <p class="moodle__subtitle">22 сентября 2026, 17:25 (длительность 00:05)</p>
            <a href="https://my.mts-link.ru/j/Ranepa/25428026479/record-new/24883158850">Запись вебинара</a>
          </li>
        </ul>
        """;

    /// <summary>
    /// Со страницы берутся только вебинары с записью: дата, длительность, ссылка и номера читаются точно
    /// </summary>
    [Fact]
    public void Only_webinars_with_a_record_are_parsed()
    {
        var webinars = WebinarParser.Parse(Page);

        Assert.Equal(2, webinars.Count);
        Assert.Equal(new DateTime(2026, 9, 7, 19, 25, 0), webinars[0].Start);
        Assert.Equal(119, webinars[0].Minutes);
        Assert.Equal("https://my.mts-link.ru/j/Ranepa/24692920129/record-new/24116923549", webinars[0].RecordUrl);
        Assert.Equal(("24692920129", "24116923549"), (webinars[0].EventId, webinars[0].SessionId));
        Assert.Equal(5, webinars[1].Minutes);
    }

    /// <summary>
    /// Занятие получает ключ из номера записи, название предмета без служебных скобок и подпись без даты
    /// </summary>
    [Fact]
    public void A_webinar_becomes_a_lesson_with_a_clean_title_and_label()
    {
        var webinars = WebinarParser.Parse(Page);

        var lecture = LessonImporter.ToLesson(webinars[0]);
        var plain = LessonImporter.ToLesson(webinars[1]);

        Assert.Equal(("e24116923549", "Гейм-дизайн", "Лекция 1"), (lecture.Id, lecture.Title, lecture.Label));
        Assert.Equal(new DateOnly(2026, 9, 7), lecture.Date);
        Assert.Equal(string.Empty, plain.Label);
    }

    /// <summary>
    /// Короткая запись сразу получает отметку «не забирать», обычная ждёт решения
    /// </summary>
    [Fact]
    public void A_short_record_is_skipped_at_first()
    {
        var webinars = WebinarParser.Parse(Page);

        Assert.Equal(LessonStateEnum.Undecided, LessonImporter.InitialState(LessonImporter.ToLesson(webinars[0]), 10));
        Assert.Equal(LessonStateEnum.Skip, LessonImporter.InitialState(LessonImporter.ToLesson(webinars[1]), 10));
    }

    /// <summary>
    /// В файле расшифровки есть заголовок занятия, резюме и реплики со временем от начала записи
    /// </summary>
    [Fact]
    public void The_transcript_file_has_a_header_a_summary_and_timed_lines()
    {
        var lesson = new Lesson("e1", "Гейм-дизайн", new DateOnly(2026, 9, 14), "Лекция 2");
        var text = TranscriptFormatter.Format(lesson, new MtsTranscript("Тема встречи: игры", [new TranscriptLine(TimeSpan.FromSeconds(3725), "Алексей", "Добрый вечер.")]));

        Assert.StartsWith("Занятие: Гейм-дизайн, Лекция 2", text, StringComparison.Ordinal);
        Assert.Contains("Дата: 14.09.2026", text, StringComparison.Ordinal);
        Assert.Contains("== Резюме ==", text, StringComparison.Ordinal);
        Assert.Contains("[01:02:05] Алексей: Добрый вечер.", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Новый список занятий дополняет старый, а отметки сохраняются и читаются по ключу
    /// </summary>
    [Fact]
    public void Lessons_are_merged_and_states_are_remembered()
    {
        var directory = Path.Combine(Path.GetTempPath(), "steward-lms-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new HomeworkStore(directory);
            store.SaveLessons([new Lesson("e1", "А", new DateOnly(2026, 9, 1))]);
            store.SaveLessons([new Lesson("e2", "Б", new DateOnly(2026, 9, 2))]);
            store.SetState("e1", LessonStateEnum.Fetch);

            Assert.Equal(["e2", "e1"], store.LoadLessons().Select(lesson => lesson.Id).ToArray());
            Assert.Equal(LessonStateEnum.Fetch, store.StateFor("e1"));
            Assert.Equal(LessonStateEnum.Undecided, store.StateFor("e2"));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Расшифровка забирается, только когда предмет отмечен «расшифровывать», у занятия нет своей отметки «не нужно» и текста ещё нет
    /// </summary>
    [Fact]
    public void A_transcript_is_fetched_only_for_subjects_marked_for_it()
    {
        var directory = Path.Combine(Path.GetTempPath(), "steward-lms-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new HomeworkStore(directory);
            var lesson = new Lesson("e1", "Гейм-дизайн", new DateOnly(2026, 9, 1), RecordUrl: "https://my.mts-link.ru/j/Ranepa/1/record-new/2");
            var other = new Lesson("e2", "Английский", new DateOnly(2026, 9, 2), RecordUrl: "https://my.mts-link.ru/j/Ranepa/3/record-new/4");

            Assert.False(store.ShouldFetch(lesson));

            store.SetSubjectState("Гейм-дизайн", LessonStateEnum.Fetch);
            Assert.True(store.ShouldFetch(lesson));
            Assert.False(store.ShouldFetch(other));

            store.SetState("e1", LessonStateEnum.Skip);
            Assert.False(store.ShouldFetch(lesson));

            store.SetState("e1", LessonStateEnum.Undecided);
            Directory.CreateDirectory(Path.GetDirectoryName(store.TranscriptPath("e1"))!);
            File.WriteAllText(store.TranscriptPath("e1"), "текст");
            Assert.False(store.ShouldFetch(lesson));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Страница заданий: сданные и несданные различаются, срок читается, у работы без срока он пустой
    /// </summary>
    [Fact]
    public void Assignments_are_parsed_with_status_and_due_date()
    {
        const string html = """
            <table class="generaltable"><tr><th>Секция</th><th>Задания</th></tr>
            <tr><td>Дизайн</td><td><a href="https://lms.ranepa.ru/mod/assign/view.php?id=1001">Задание 1</a></td><td>пятница, 23 октября 2026, 00:00</td><td>Отправлено для оценивания</td><td>-</td></tr>
            <tr><td>Баланс</td><td><a href="https://lms.ranepa.ru/mod/assign/view.php?id=1002">Задание 2</a></td><td>-</td><td>Нет ответа на задание</td><td>-</td></tr>
            <tr><td>Звук</td><td><a href="https://lms.ranepa.ru/mod/assign/view.php?id=1003">Задание 3</a></td><td>суббота, 31 октября 2026, 20:34</td><td>Черновик (не отправлено)</td><td></td></tr>
            </table>
            """;

        var items = ActivityParser.ParseAssignments(html, 5, "Гейм-дизайн");

        Assert.Equal([true, false, false], items.Select(item => item.Done).ToArray());
        Assert.Equal(new DateTime(2026, 10, 23), items[0].Due);
        Assert.Null(items[1].Due);
        Assert.Equal("lms:assign:1002", items[1].ExternalId);
        Assert.Equal("Дизайн", items[0].Section);
    }

    /// <summary>
    /// Тест считается сданным, когда у него есть оценка, а прочерк значит «не пройден»
    /// </summary>
    [Fact]
    public void A_quiz_is_done_when_it_has_a_grade()
    {
        const string html = """
            <table><tr><td>Тема 1</td><td><a href="https://lms.ranepa.ru/mod/quiz/view.php?id=2001">Тест 1</a></td><td>Нет даты закрытия</td><td>100,00/100,00</td></tr>
            <tr><td>Тема 2</td><td><a href="https://lms.ranepa.ru/mod/quiz/view.php?id=2002">Тест 2</a></td><td>среда, 30 декабря 2026, 20:36</td><td>-</td></tr></table>
            """;

        var items = ActivityParser.ParseQuizzes(html, 5, "C#");

        Assert.Equal([true, false], items.Select(item => item.Done).ToArray());
        Assert.Equal("lms:quiz:2002", items[1].ExternalId);
        Assert.Equal(new DateTime(2026, 12, 30, 20, 36, 0), items[1].Due);
    }

    /// <summary>
    /// Работа с оценкой считается сданной, даже если состояние написано непривычными словами, а тест в строке без столбца оценки читается и считается непройденным
    /// </summary>
    [Fact]
    public void A_grade_means_submitted_and_a_quiz_row_without_a_grade_column_still_parses()
    {
        const string assignments = """
            <table><tr><td>Тема</td><td><a href="https://lms.ranepa.ru/mod/assign/view.php?id=3001">Задание</a></td><td>-</td><td>Принято преподавателем</td><td>85,00 / 100,00</td></tr></table>
            """;
        const string quizzes = """
            <table><tr><td>Тема 1</td><td><a href="https://lms.ranepa.ru/mod/quiz/view.php?id=4001">Тест</a></td><td>Нет даты закрытия</td></tr></table>
            """;

        Assert.True(ActivityParser.ParseAssignments(assignments, 5, "C#").Single().Done);

        var quiz = ActivityParser.ParseQuizzes(quizzes, 5, "C#").Single();

        Assert.False(quiz.Done);
        Assert.Equal("lms:quiz:4001", quiz.ExternalId);
    }

    /// <summary>
    /// Задачи заводятся только по несданным работам предметов «расшифровывать», а открытая задача на сданную работу закрывается
    /// </summary>
    [Fact]
    public void The_plan_creates_for_open_work_of_active_subjects_and_closes_for_submitted_work()
    {
        var open = new LmsActivity("1", ActivityKindEnum.Assignment, 5, "Гейм-дизайн", string.Empty, "З1", null, "Нет ответа", string.Empty, false, "u1");
        var other = new LmsActivity("2", ActivityKindEnum.Assignment, 6, "Английский", string.Empty, "З2", null, "Нет ответа", string.Empty, false, "u2");
        var submitted = new LmsActivity("3", ActivityKindEnum.Assignment, 5, "Гейм-дизайн", string.Empty, "З3", null, "Отправлено", string.Empty, true, "u3");
        var already = new LmsActivity("4", ActivityKindEnum.Quiz, 5, "Гейм-дизайн", string.Empty, "Т4", null, "Не пройден", string.Empty, false, "u4");
        var tasks = new[] { new SingularityTaskInfo("t3", "lms:assign:3", 0), new SingularityTaskInfo("t4", "lms:quiz:4", 0, Removed: true) };

        var plan = ActivityTaskSync.Build([open, other, submitted, already], tasks, subject => subject == "Гейм-дизайн" ? LessonStateEnum.Fetch : LessonStateEnum.Undecided);

        Assert.Equal(["lms:assign:1"], plan.ToCreate.Select(activity => activity.ExternalId).ToArray());
        Assert.Equal(["t3"], plan.ToClose.Select(pair => pair.Task.Id).ToArray());
    }

    /// <summary>
    /// Занятие, заказанное кнопкой «Расшифровать», забирается даже у предмета с отметкой «не нужно», а формат занятия по умолчанию берётся у предмета
    /// </summary>
    [Fact]
    public void A_lesson_ordered_by_hand_is_fetched_even_if_the_subject_is_not_needed()
    {
        var directory = Path.Combine(Path.GetTempPath(), "steward-lms-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new HomeworkStore(directory);
            var lesson = new Lesson("e1", "Английский", new DateOnly(2026, 9, 1), RecordUrl: "https://my.mts-link.ru/j/Ranepa/1/record-new/2");
            store.SaveLessons([lesson]);
            store.SetSubjectState("Английский", LessonStateEnum.Skip);
            Assert.False(store.ShouldFetch(lesson));

            store.SetState("e1", LessonStateEnum.Fetch);
            Assert.True(store.ShouldFetch(lesson));

            Assert.Equal(HomeworkFormatEnum.Word, store.FormatFor("e1"));
            store.SetSubjectFormat("Английский", HomeworkFormatEnum.Text);
            Assert.Equal(HomeworkFormatEnum.Text, store.FormatFor("e1"));
            store.SetFormat("e1", HomeworkFormatEnum.Code);
            Assert.Equal(HomeworkFormatEnum.Code, store.FormatFor("e1"));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
