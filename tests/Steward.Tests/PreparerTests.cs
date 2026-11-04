using System.Text.RegularExpressions;
using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Homework.Storage;

namespace Steward.Tests;

/// <summary>
/// Автоподготовка работ в фоновой задаче: отбор заданий, лимит, подтверждение, повторы после ошибки, проверка, замечания и образцы
/// </summary>
public partial class PreparerTests : IDisposable
{
    /// <summary>
    /// Предмет занятий в тестах
    /// </summary>
    private const string SUBJECT = "Физика";

    /// <summary>
    /// Папка домашних заданий на время теста
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "steward-prepare-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Хранилище домашних заданий в папке теста
    /// </summary>
    private readonly HomeworkStore _store;

    /// <summary>
    /// Методические указания и образцы в папке теста
    /// </summary>
    private readonly GuideStore _guides;

    /// <summary>
    /// Замечания владельца в папке теста
    /// </summary>
    private readonly RemarkStore _remarks;

    /// <summary>
    /// Агент-заглушка
    /// </summary>
    private readonly StubAgent _agent;

    /// <summary>
    /// Создаёт хранилища и отмечает предмет «расшифровывать» в формате MD
    /// </summary>
    public PreparerTests()
    {
        _store = new HomeworkStore(_root);
        _guides = new GuideStore(_root);
        _remarks = new RemarkStore(Path.Combine(_root, "remarks"));
        _agent = new StubAgent();
        _store.SetSubjectState(SUBJECT, LessonStateEnum.Fetch);
        _store.SetSubjectFormat(SUBJECT, HomeworkFormatEnum.Text);
        _store.SetSubjectAutoPrepare(SUBJECT, true);
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
    /// Новый подготовщик: как отдельный запуск фоновой задачи
    /// </summary>
    /// <returns>Подготовщик с агентом-заглушкой</returns>
    private HomeworkPreparer Preparer() => new(_store, _guides, _remarks, _agent.Run) { Clock = new FixedClock(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.FromHours(3))) };

    /// <summary>
    /// Записывает занятие и его задания
    /// </summary>
    /// <param name="id">Ключ занятия</param>
    /// <param name="day">День октября 2026</param>
    /// <param name="itemsJson">Задания в JSON, как их пишет агент</param>
    /// <param name="subject">Предмет</param>
    /// <returns>Занятие</returns>
    private Lesson AddLesson(string id, int day, string itemsJson, string subject = SUBJECT)
    {
        var lesson = new Lesson(id, subject, new DateOnly(2026, 10, day));
        _store.SaveLessons([lesson]);
        Directory.CreateDirectory(Path.Combine(_root, "items"));
        File.WriteAllText(_store.ItemsPath(id), itemsJson);

        return lesson;
    }

    /// <summary>
    /// Кладёт готовый файл работы в папку занятия
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="name">Имя файла</param>
    /// <param name="text">Содержимое</param>
    /// <returns>Путь к файлу</returns>
    private string AddWork(string lessonId, string name, string text)
    {
        var folder = _store.DraftsDirectory(lessonId);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, text);

        return path;
    }

    /// <summary>
    /// Готовятся только несданные задания без файла работы, свежие занятия первыми и не больше лимита; файл владельца не трогается
    /// </summary>
    [Fact]
    public async Task Only_pending_items_without_files_are_prepared_up_to_the_limit()
    {
        AddLesson("old", 10, """[{"title": "Старое задание"}]""");
        AddLesson("new", 20, """[{"title": "Первое"}, {"title": "Второе"}, {"title": "Третье"}]""");
        var own = AddWork("new", "Задание 2.md", "моя работа");

        var report = await Preparer().RunAsync(2, CancellationToken.None);

        Assert.Equal(["Физика, Первое", "Физика, Третье"], report.Prepared);
        Assert.Equal(2, _agent.WorkPrompts.Count);
        Assert.NotNull(_store.SampleCandidate("new", 1));
        Assert.NotNull(_store.SampleCandidate("new", 3));
        Assert.Null(_store.SampleCandidate("old", 1));
        Assert.Equal("моя работа", File.ReadAllText(own));
    }

    /// <summary>
    /// Сданные (по файлу заданий, по отметке и по LMS), архивные задания и предмет «Не готовить» не тратят агента; занятие с несданным заданием агента вызывает
    /// </summary>
    [Fact]
    public async Task Done_archived_submitted_and_skip_format_subjects_are_ignored()
    {
        AddLesson("l1", 20, """[{"title": "Сдано по LMS", "done": true}, {"title": "В архиве"}, {"title": "Отмечено сданным"}, {"title": "Сдано позже", "activity": "77"}]""");
        _store.SetArchived("l1", 2, true);
        _store.SetSubmitted("l1", 3, true);
        File.WriteAllText(Path.Combine(_root, "activities.json"), """[{"subject": "Физика", "name": "Работа", "kind": 0, "done": true, "id": "77"}]""");
        _store.SetSubjectState("Химия", LessonStateEnum.Fetch);
        _store.SetSubjectFormat("Химия", HomeworkFormatEnum.Skip);
        _store.SetSubjectAutoPrepare("Химия", true);
        AddLesson("c1", 21, """[{"title": "Не готовить"}]""", "Химия");

        var report = await Preparer().RunAsync(5, CancellationToken.None);

        Assert.Empty(_agent.Prompts);
        Assert.Empty(report.Prepared);
        Assert.Empty(report.Failed);

        // Положительная проверка: несданное задание того же занятия готовится
        File.WriteAllText(_store.ItemsPath("l1"), """[{"title": "Сдано по LMS", "done": true}, {"title": "В архиве"}, {"title": "Отмечено сданным"}, {"title": "Сдано позже", "activity": "77"}, {"title": "Новое"}]""");
        var second = await Preparer().RunAsync(5, CancellationToken.None);

        Assert.Equal(["Физика, Новое"], second.Prepared);
        Assert.Single(_agent.WorkPrompts);
    }

    /// <summary>
    /// Низкая уверенность, ненайденная цитата и задание без связи с работой LMS ждут подтверждения; подтверждённое и надёжное готовятся
    /// </summary>
    [Fact]
    public async Task Items_needing_confirmation_are_not_prepared_and_listed_as_waiting()
    {
        AddLesson("l1", 20, """
            [
              {"title": "Низкая", "evidence": "сделайте нормализацию", "confidence": "low", "doubt": "не тот предмет", "activity": "55"},
              {"title": "Без цитаты", "evidence": "напишите эссе", "confidence": "high", "activity": "55"},
              {"title": "Без работы LMS", "evidence": "сделайте нормализацию", "confidence": "high"},
              {"title": "Надёжное", "evidence": "сделайте нормализацию", "confidence": "high", "activity": "55"},
              {"title": "Подтверждённое", "evidence": "что-то", "confidence": "low"}
            ]
            """);
        Directory.CreateDirectory(Path.Combine(_root, "transcripts"));
        File.WriteAllText(_store.TranscriptPath("l1"), "Преподаватель: Сделайте  нормализацию до пятницы.");
        Directory.CreateDirectory(_store.AssignmentDirectory("55"));
        File.WriteAllText(Path.Combine(_store.AssignmentDirectory("55"), "assignment.md"), "Условие работы");
        _store.Confirm("l1", 5);

        var report = await Preparer().RunAsync(10, CancellationToken.None);

        Assert.Equal(["Физика, Надёжное", "Физика, Подтверждённое"], report.Prepared);
        Assert.Equal(3, report.Waiting.Count);
        Assert.Contains(report.Waiting, line => line.StartsWith("Физика, Низкая: ", StringComparison.Ordinal) && line.Contains("не тот предмет", StringComparison.Ordinal));
        Assert.Contains(report.Waiting, line => line.StartsWith("Физика, Без цитаты: ", StringComparison.Ordinal));
        Assert.Contains(report.Waiting, line => line.StartsWith("Физика, Без работы LMS: ", StringComparison.Ordinal) && line.Contains("условие", StringComparison.Ordinal));
        Assert.Equal(2, _agent.WorkPrompts.Count);
    }

    /// <summary>
    /// Падающий агент вызывается не больше трёх раз подряд, даже если каждый запуск идёт новым процессом; дальше задание ждёт
    /// </summary>
    [Fact]
    public async Task A_failing_agent_is_retried_at_most_three_times_in_a_row()
    {
        AddLesson("l1", 20, """[{"title": "Эссе"}]""");
        _agent.Error = "Claude завершился с кодом 1";

        PrepareReport last = null!;
        for (var run = 0; run < 5; run++)
            last = await Preparer().RunAsync(2, CancellationToken.None);

        Assert.Equal(3, _agent.WorkPrompts.Count);
        Assert.Empty(last.Failed);
        Assert.Contains(last.Waiting, line => line.StartsWith("Физика, Эссе: ", StringComparison.Ordinal));
        Assert.Null(_store.SampleCandidate("l1", 1));
        Assert.True(File.Exists(Path.Combine(_root, "prepare-attempts.json")));
    }

    /// <summary>
    /// Удачная подготовка сбрасывает счётчик попыток: после двух ошибок и успеха новые ошибки снова получают три попытки
    /// </summary>
    [Fact]
    public async Task A_success_resets_the_attempt_counter()
    {
        AddLesson("l1", 20, """[{"title": "Эссе"}]""");
        _agent.Error = "сбой";
        await Preparer().RunAsync(2, CancellationToken.None);
        await Preparer().RunAsync(2, CancellationToken.None);
        _agent.Error = string.Empty;

        var ok = await Preparer().RunAsync(2, CancellationToken.None);

        Assert.Equal(["Физика, Эссе"], ok.Prepared);
        Assert.DoesNotContain("l1:1", File.ReadAllText(Path.Combine(_root, "prepare-attempts.json")), StringComparison.Ordinal);

        File.Delete(_store.SampleCandidate("l1", 1)!);
        _agent.Error = "сбой";
        for (var run = 0; run < 4; run++)
            await Preparer().RunAsync(2, CancellationToken.None);

        Assert.Equal(3 + 3, _agent.WorkPrompts.Count);
    }

    /// <summary>
    /// Испорченный или пустой файл попыток не роняет запуск и считается пустым
    /// </summary>
    [Fact]
    public async Task A_corrupt_attempts_file_is_tolerated()
    {
        AddLesson("l1", 20, """[{"title": "Эссе"}]""");
        File.WriteAllText(Path.Combine(_root, "prepare-attempts.json"), "{не json");

        var report = await Preparer().RunAsync(2, CancellationToken.None);

        Assert.Equal(["Физика, Эссе"], report.Prepared);
    }

    /// <summary>
    /// Если агент оставил недописанный файл и вернул ошибку, файл убирается и «Готово» не наступает
    /// </summary>
    [Fact]
    public async Task A_failed_attempt_leaves_no_work_file()
    {
        AddLesson("l1", 20, """[{"title": "Эссе"}]""");
        _agent.Error = "оборвался";
        _agent.Leftover = Path.Combine(_store.DraftsDirectory("l1"), "Задание 1. Эссе.md");

        var report = await Preparer().RunAsync(2, CancellationToken.None);

        Assert.Single(report.Failed);
        Assert.Null(_store.SampleCandidate("l1", 1));
        Assert.False(File.Exists(_agent.Leftover));
        Assert.Equal("половина", File.ReadAllText(Path.Combine(_store.DraftsDirectory("l1"), HomeworkStore.FAILED_FOLDER, "Задание 1. Эссе.md")));
    }

    /// <summary>
    /// Файл неудачной попытки не затирает одноимённый файл прошлой неудачи в <c>.failed</c>, а недописанный <c>.partial</c> удаляется, а не откладывается
    /// </summary>
    [Fact]
    public async Task Failed_attempts_never_overwrite_set_aside_files_and_partials_are_deleted()
    {
        AddLesson("l1", 20, """[{"title": "Эссе"}]""");
        var failed = Path.Combine(_store.DraftsDirectory("l1"), HomeworkStore.FAILED_FOLDER);
        Directory.CreateDirectory(failed);
        File.WriteAllText(Path.Combine(failed, "Задание 1. Эссе.md"), "прошлая неудача");
        _agent.Error = "оборвался";
        _agent.Leftover = Path.Combine(_store.DraftsDirectory("l1"), "Задание 1. Эссе.md");
        var partial = Path.Combine(_store.DraftsDirectory("l1"), "Задание 1. Эссе.md.partial");
        _agent.AlsoLeftover = partial;

        await Preparer().RunAsync(2, CancellationToken.None);

        Assert.Equal("прошлая неудача", File.ReadAllText(Path.Combine(failed, "Задание 1. Эссе.md")));
        Assert.Equal("половина", File.ReadAllText(Path.Combine(failed, "Задание 1. Эссе (2).md")));
        Assert.False(File.Exists(partial));
        Assert.Empty(Directory.EnumerateFiles(failed, "*.partial"));
        Assert.Null(_store.SampleCandidate("l1", 1));
    }

    /// <summary>
    /// Недописанный файл прошлого запуска (.partial) не считается готовой работой: агент запускается заново
    /// </summary>
    [Fact]
    public async Task A_stale_partial_file_does_not_count_as_a_finished_work()
    {
        AddLesson("l1", 20, """[{"title": "Эссе"}]""");
        AddWork("l1", "Задание 1. Эссе.md.partial", "хвост");

        var report = await Preparer().RunAsync(2, CancellationToken.None);

        Assert.Equal(["Физика, Эссе"], report.Prepared);
        Assert.Single(_agent.WorkPrompts);
        Assert.Equal(Path.Combine(_store.DraftsDirectory("l1"), "Задание 1. Эссе.md"), _store.SampleCandidate("l1", 1));
    }

    /// <summary>
    /// Готовая работа сразу получает отчёт проверки
    /// </summary>
    [Fact]
    public async Task A_prepared_work_gets_an_automatic_check_report()
    {
        AddLesson("l1", 20, """[{"title": "Эссе"}]""");

        await Preparer().RunAsync(2, CancellationToken.None);

        Assert.True(File.Exists(_store.CheckReportPath("l1")));
        Assert.Single(_agent.CheckPrompts);
        Assert.Contains("Задание 1. Эссе.md", _agent.CheckPrompts[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// Агент получает замечания владельца и образцы предмета в тексте просьбы
    /// </summary>
    [Fact]
    public async Task Remarks_and_samples_reach_the_agent_prompt()
    {
        AddLesson("l1", 20, """[{"title": "Эссе"}]""");
        _remarks.Add(new Remark("r1", new DateOnly(2026, 10, 1), SUBJECT, "l0", 1, "Без воды и вступлений", RemarkKindEnum.Style, Global: false));
        var source = Path.Combine(_root, "образец.txt");
        File.WriteAllText(source, "текст образца");
        var sample = _guides.AddSample(SUBJECT, "l0", 1, source, fromUser: false);

        await Preparer().RunAsync(2, CancellationToken.None);

        Assert.Single(_agent.WorkPrompts);
        Assert.Contains("Без воды и вступлений", _agent.WorkPrompts[0], StringComparison.Ordinal);
        Assert.Contains(sample.Path, _agent.WorkPrompts[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// Работа сданного задания один раз становится образцом предмета: удалённый владельцем образец не возвращается
    /// </summary>
    [Fact]
    public async Task A_submitted_work_becomes_a_sample_once()
    {
        AddLesson("l1", 20, """[{"title": "Эссе"}, {"title": "Доклад"}]""");
        AddWork("l1", "Задание 1. Эссе.md", "сданная работа");
        _store.SetSubmitted("l1", 1, true);

        await Preparer().RunAsync(0, CancellationToken.None);

        var sample = Assert.Single(_guides.Load(), entry => entry.Id == "sample:l1:1");
        Assert.Equal("сданная работа", File.ReadAllText(sample.Path));
        Assert.DoesNotContain(_guides.Load(), entry => entry.Id == "sample:l1:2");

        _guides.Remove(sample.Id);
        await Preparer().RunAsync(0, CancellationToken.None);

        Assert.DoesNotContain(_guides.Load(), entry => entry.Id == "sample:l1:1");
    }

    /// <summary>
    /// Пока один запуск готовит работу, второй ничего не запускает и сообщает, что подготовка уже идёт
    /// </summary>
    [Fact]
    public async Task Two_runs_at_once_do_not_prepare_the_same_work()
    {
        AddLesson("l1", 20, """[{"title": "Эссе"}]""");
        _agent.Gate = new TaskCompletionSource();

        var first = Preparer().RunAsync(2, CancellationToken.None);
        await _agent.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = await Preparer().RunAsync(2, CancellationToken.None);
        _agent.Gate.SetResult();
        var done = await first;

        Assert.True(second.Busy);
        Assert.Empty(second.Prepared);
        Assert.Equal(["Физика, Эссе"], done.Prepared);
        Assert.Single(_agent.WorkPrompts);
    }

    /// <summary>
    /// Отмена во время работы агента не засчитывается как попытка и останавливает запуск
    /// </summary>
    [Fact]
    public async Task Cancellation_does_not_count_as_an_attempt()
    {
        AddLesson("l1", 20, """[{"title": "Эссе"}]""");
        using var cancel = new CancellationTokenSource();
        _agent.OnRun = () =>
        {
            cancel.Cancel();

            return "Запуск отменён";
        };

        var report = await Preparer().RunAsync(2, cancel.Token);

        Assert.True(report.Cancelled);
        var attempts = Path.Combine(_root, "prepare-attempts.json");
        Assert.True(!File.Exists(attempts) || !File.ReadAllText(attempts).Contains("l1:1", StringComparison.Ordinal));
    }

    /// <summary>
    /// Строки уведомления о готовой работе пишутся и читаются из вывода в одном виде
    /// </summary>
    [Fact]
    public void Ready_work_notices_are_formatted_and_read_back()
    {
        var report = new PrepareReport(["Физика, Эссе"], [], []);

        Assert.Equal(["Готова работа: Физика, Эссе"], report.Notices());
        Assert.Equal(["Физика, Эссе", "Химия, Доклад"], HomeworkPreparer.ReadyWorks("29.10.2026 10:00:00  Готова работа: Физика, Эссе\nдругая строка\nГотова работа: Химия, Доклад\r\n29.10.2026 10:00:00  Готова работа: Физика, Эссе"));
        Assert.Empty(HomeworkPreparer.ReadyWorks("29.10.2026 10:00:00  Ждёт владельца: Физика, Готова работа: Поддельная"));
        Assert.Empty(HomeworkPreparer.ReadyWorks(null));
    }

    /// <summary>
    /// Название задания с переводом строки не может подделать отдельную строку уведомления: в отчёт и уведомление оно попадает одной строкой
    /// </summary>
    [Fact]
    public async Task A_title_with_a_newline_cannot_forge_a_notice()
    {
        AddLesson("l1", 20, """[{"title": "Эссе\nГотова работа: Поддельная"}]""");

        var report = await Preparer().RunAsync(2, CancellationToken.None);

        var notice = Assert.Single(report.Notices());
        Assert.DoesNotContain('\n', notice);
        Assert.Equal(["Физика, Эссе Готова работа: Поддельная"], HomeworkPreparer.ReadyWorks(string.Join("\n", report.Notices())));
    }

    /// <summary>
    /// Каждая готовая работа сообщается сразу после записи, по одному разу
    /// </summary>
    [Fact]
    public async Task Each_ready_work_is_reported_as_soon_as_it_is_ready()
    {
        AddLesson("l1", 20, """[{"title": "Первое"}, {"title": "Второе"}]""");
        var heard = new List<(string Work, int WorksSoFar)>();

        await Preparer().RunAsync(2, CancellationToken.None, work => heard.Add((work, _agent.WorkPrompts.Count)));

        Assert.Equal([("Физика, Первое", 1), ("Физика, Второе", 2)], heard);
    }

    /// <summary>
    /// Срок шага подготовки: запуск останавливается, готовая до этого работа остаётся в отчёте, прерванная попытка не засчитывается
    /// </summary>
    [Fact]
    public async Task A_deadline_returns_the_partial_report_without_counting_the_attempt()
    {
        AddLesson("l1", 20, """[{"title": "Первое"}, {"title": "Второе"}]""");
        _agent.HangOnWork = 2;

        var report = await Preparer().RunWithBudgetAsync(5, TimeSpan.FromMilliseconds(300), null, CancellationToken.None);

        Assert.True(report.Cancelled);
        Assert.Equal(["Физика, Первое"], report.Prepared);
        Assert.Empty(report.Failed);
        Assert.Null(_store.SampleCandidate("l1", 2));
        var attempts = Path.Combine(_root, "prepare-attempts.json");
        Assert.True(!File.Exists(attempts) || !File.ReadAllText(attempts).Contains("l1:2", StringComparison.Ordinal));

        // Следующий запуск готовит прерванное задание
        _agent.HangOnWork = 0;
        var next = await Preparer().RunAsync(5, CancellationToken.None);
        Assert.Equal(["Физика, Второе"], next.Prepared);
    }

    /// <summary>
    /// В формате кода агент пишет прямо в папку: недописанный файл прерванной по сроку попытки удаляется
    /// </summary>
    [Fact]
    public async Task A_code_file_half_written_before_the_deadline_is_removed()
    {
        _store.SetSubjectFormat(SUBJECT, HomeworkFormatEnum.Code);
        AddLesson("l1", 20, """[{"title": "Скрипт"}]""");
        Directory.CreateDirectory(_store.DraftsDirectory("l1"));
        _agent.HangOnWork = 1;
        _agent.HangLeftover = Path.Combine(_store.DraftsDirectory("l1"), "Задание 1.py");

        var report = await Preparer().RunWithBudgetAsync(2, TimeSpan.FromMilliseconds(300), null, CancellationToken.None);

        Assert.True(report.Cancelled);
        Assert.Empty(report.Prepared);
        Assert.False(File.Exists(_agent.HangLeftover));
        Assert.Null(_store.SampleCandidate("l1", 1));
        Assert.True(File.Exists(Path.Combine(_store.DraftsDirectory("l1"), HomeworkStore.FAILED_FOLDER, "Задание 1.py")));
    }

    /// <summary>
    /// Файл, который остановленный агент ещё держит открытым после отмены, удаляется повтором, как только его отпустят
    /// </summary>
    [Fact]
    public async Task A_code_file_still_held_after_the_deadline_is_removed_once_released()
    {
        _store.SetSubjectFormat(SUBJECT, HomeworkFormatEnum.Code);
        AddLesson("l1", 20, """[{"title": "Скрипт"}]""");
        Directory.CreateDirectory(_store.DraftsDirectory("l1"));
        _agent.HangOnWork = 1;
        _agent.HangLeftover = Path.Combine(_store.DraftsDirectory("l1"), "Задание 1.py");
        _agent.HoldAfterCancelMs = 500;

        var report = await Preparer().RunWithBudgetAsync(2, TimeSpan.FromMilliseconds(300), null, CancellationToken.None);

        Assert.True(report.Cancelled);
        Assert.False(File.Exists(_agent.HangLeftover));
        Assert.Null(_store.SampleCandidate("l1", 1));
        Assert.True(File.Exists(Path.Combine(_store.DraftsDirectory("l1"), HomeworkStore.FAILED_FOLDER, "Задание 1.py")));
    }

    /// <summary>
    /// Фоновая подготовка берёт только занятия не старше заданного числа дней: занятие ровно на границе готовится, более старое агента не тратит, но образец его сданной работы регистрируется
    /// </summary>
    [Fact]
    public async Task Only_lessons_within_the_age_limit_are_prepared()
    {
        AddLesson("recent", 25, """[{"title": "Свежее"}]""");
        AddLesson("edge", 9, """[{"title": "На границе"}]""");
        AddLesson("old", 8, """[{"title": "Старое"}, {"title": "Сданное"}]""");
        AddWork("old", "Задание 2. Сданное.md", "сданная работа");
        _store.SetSubmitted("old", 2, true);

        var report = await Preparer().RunAsync(10, CancellationToken.None, maxAgeDays: 21);

        Assert.Equal(["Физика, Свежее", "Физика, На границе"], report.Prepared);
        Assert.Equal(2, _agent.WorkPrompts.Count);
        Assert.DoesNotContain(_agent.WorkPrompts, prompt => prompt.Contains("Старое", StringComparison.Ordinal));
        Assert.Null(_store.SampleCandidate("old", 1));
        Assert.Contains(_guides.Load(), entry => entry.Id == "sample:old:2");
    }

    /// <summary>
    /// Кнопка «Подготовить задание» срок не учитывает: старое занятие готовится по просьбе владельца
    /// </summary>
    [Fact]
    public async Task The_manual_button_prepares_an_old_lesson()
    {
        var lesson = AddLesson("old", 1, """[{"title": "Старое"}]""");

        var error = await Preparer().PrepareLessonAsync(lesson, _store.ReadItems("old")!, HomeworkFormatEnum.Text, () => string.Empty, null, CancellationToken.None);

        Assert.Equal(string.Empty, error);
        Assert.NotNull(_store.SampleCandidate("old", 1));
    }

    /// <summary>
    /// Если снимок работ LMS не читается, запуск подготовки не падает: отчёт возвращается с ошибкой, и остальные шаги фоновой задачи идут дальше
    /// </summary>
    [Fact]
    public async Task An_unreadable_activity_snapshot_returns_a_failed_report()
    {
        AddLesson("l1", 20, """[{"title": "Эссе"}]""");
        File.WriteAllText(Path.Combine(_root, "activities.json"), """[{"kind": 1.5}]""");

        var report = await Preparer().RunAsync(2, CancellationToken.None);

        Assert.Single(report.Failed);
        Assert.Empty(_agent.Prompts);
        Assert.False(report.Busy);
    }

    /// <summary>
    /// Пока идёт фоновая подготовка, «Удалить» ничего не удаляет и сообщает об этом; после подготовки удаляет папку работ занятия
    /// </summary>
    [Fact]
    public async Task Deleting_drafts_is_refused_while_a_background_prepare_runs()
    {
        AddLesson("l1", 20, """[{"title": "Эссе"}]""");
        AddWork("l9", "Задание 1.md", "работа");
        _agent.Gate = new TaskCompletionSource();

        var run = Preparer().RunAsync(2, CancellationToken.None);
        await _agent.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var refused = Preparer().DeleteDrafts("l9");
        _agent.Gate.SetResult();
        await run;

        Assert.Equal(HomeworkPreparer.DELETE_BUSY_MESSAGE, refused);
        Assert.True(Directory.Exists(_store.DraftsDirectory("l9")));
        Assert.Equal(string.Empty, Preparer().DeleteDrafts("l9"));
        Assert.False(Directory.Exists(_store.DraftsDirectory("l9")));
    }

    /// <summary>
    /// Сбой записи учёта после удачной работы не теряет работу из отчёта и не возвращает лимит
    /// </summary>
    [Fact]
    public async Task A_ledger_failure_after_a_ready_work_keeps_the_work_and_the_spent_limit()
    {
        AddLesson("old", 10, """[{"title": "Другое"}]""");
        AddLesson("l1", 20, """[{"title": "Эссе"}]""");
        var attempts = Path.Combine(_root, "prepare-attempts.json");
        // Файл учёта только для чтения: сброс счётчика после удачной работы не запишется
        _agent.AfterWork = () => File.SetAttributes(attempts, FileAttributes.ReadOnly);
        try
        {
            var report = await Preparer().RunAsync(1, CancellationToken.None);

            Assert.Equal(["Физика, Эссе"], report.Prepared);
            Assert.Single(_agent.WorkPrompts);
        }
        finally
        {
            File.SetAttributes(attempts, FileAttributes.Normal);
        }
    }

    /// <summary>
    /// Часы с постоянным временем
    /// </summary>
    /// <param name="now">Текущее время</param>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        /// <summary>
        /// Текущее время в UTC
        /// </summary>
        /// <returns>Заданное время</returns>
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

        /// <summary>
        /// Часовой пояс: UTC, чтобы граница срока не зависела от пояса машины
        /// </summary>
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    /// <summary>
    /// Агент-заглушка: запоминает просьбы, пишет работу и отчёт проверки по путям из просьбы
    /// </summary>
    private sealed partial class StubAgent
    {
        /// <summary>
        /// Все просьбы по порядку
        /// </summary>
        public List<string> Prompts { get; } = [];

        /// <summary>
        /// Просьбы подготовить работу
        /// </summary>
        public List<string> WorkPrompts => [.. Prompts.Where(prompt => !prompt.StartsWith("Проверь готовую", StringComparison.Ordinal))];

        /// <summary>
        /// Просьбы проверить работу
        /// </summary>
        public List<string> CheckPrompts => [.. Prompts.Where(prompt => prompt.StartsWith("Проверь готовую", StringComparison.Ordinal))];

        /// <summary>
        /// Ошибка, которую возвращает подготовка, или пусто для успеха
        /// </summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// Файл, который агент оставляет перед ошибкой, или <see langword="null"/>
        /// </summary>
        public string? Leftover { get; set; }

        /// <summary>
        /// Второй файл, который агент оставляет перед ошибкой, или <see langword="null"/>
        /// </summary>
        public string? AlsoLeftover { get; set; }

        /// <summary>
        /// Если задано, подготовка ждёт его завершения
        /// </summary>
        public TaskCompletionSource? Gate { get; set; }

        /// <summary>
        /// Завершается, когда подготовка началась
        /// </summary>
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Если задано, подготовка возвращает его результат вместо записи работы
        /// </summary>
        public Func<string>? OnRun { get; set; }

        /// <summary>
        /// Номер просьбы о работе (с единицы), на которой агент зависает до отмены; 0, чтобы не зависать
        /// </summary>
        public int HangOnWork { get; set; }

        /// <summary>
        /// Файл, который зависший агент успевает начать писать, или <see langword="null"/>
        /// </summary>
        public string? HangLeftover { get; set; }

        /// <summary>
        /// Сколько миллисекунд зависший агент ещё держит файл после отмены
        /// </summary>
        public int HoldAfterCancelMs { get; set; }

        /// <summary>
        /// Вызывается после удачно записанной работы
        /// </summary>
        public Action? AfterWork { get; set; }

        /// <summary>
        /// Сколько просьб о работе пришло
        /// </summary>
        private int _works;

        /// <summary>
        /// Запуск агента
        /// </summary>
        /// <param name="prompt">Просьба</param>
        /// <param name="workDirectory">Рабочая папка</param>
        /// <param name="extraDirectories">Папки для чтения</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Пусто при успехе, иначе ошибка</returns>
        public async Task<string> Run(string prompt, string workDirectory, IReadOnlyList<string> extraDirectories, CancellationToken cancellationToken)
        {
            lock (Prompts)
                Prompts.Add(prompt);

            if (prompt.StartsWith("Проверь готовую", StringComparison.Ordinal))
            {
                var report = ReportPath().Match(prompt).Groups[1].Value;
                File.WriteAllText(report, "## Соответствие условию");

                return string.Empty;
            }

            Entered.TrySetResult();
            if (++_works == HangOnWork)
            {
                // Как умирающий процесс: файл открыт без права удаления и отпускается чуть позже отмены
                var held = HangLeftover is null ? null : new FileStream(HangLeftover, FileMode.Create, FileAccess.Write, FileShare.Read);
                held?.Write("print("u8);
                held?.Flush();

                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    if (held is not null)
                        _ = Task.Delay(HoldAfterCancelMs, CancellationToken.None).ContinueWith(_ => held.Dispose(), TaskScheduler.Default);

                    return "Запуск отменён";
                }
            }

            if (Gate is not null)
                await Gate.Task;

            if (OnRun is not null)
                return OnRun();

            if (Leftover is not null)
                File.WriteAllText(Leftover, "половина");

            if (AlsoLeftover is not null)
                File.WriteAllText(AlsoLeftover, "хвост");

            if (Error.Length > 0)
                return Error;

            var lines = prompt.Replace("\r", string.Empty).Split('\n');
            var index = Array.FindIndex(lines, line => line.Contains("путь на следующей строке", StringComparison.Ordinal));
            File.WriteAllText(lines[index + 1].Trim(), "# Работа\n\nТекст работы.");
            AfterWork?.Invoke();

            return string.Empty;
        }

        /// <summary>
        /// Путь к отчёту в просьбе о проверке
        /// </summary>
        /// <returns>Регулярное выражение</returns>
        [GeneratedRegex(@"в файл (.+?) с четырьмя разделами")]
        private static partial Regex ReportPath();
    }
}
