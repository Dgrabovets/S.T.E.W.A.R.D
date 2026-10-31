using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Homework.Storage;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Проверяет единый конвейер домашних заданий: источник и цитату задания
/// </summary>
public class PipelineTests
{
    /// <summary>
    /// Занятие 22 октября 2026
    /// </summary>
    private static readonly Lesson Sample = new("db-1022", "Базы данных: нормализация", new DateOnly(2026, 10, 22));

    /// <summary>
    /// Цитата находится в расшифровке без учёта регистра, пробелов и вида кавычек; пустая и отсутствующая цитаты не находятся
    /// </summary>
    [Fact]
    public void Evidence_is_found_ignoring_case_spaces_and_quotes()
    {
        const string transcript = "Преподаватель: итак,\nсделайте   \"Нормализацию до третьей формы\"\r\nи сдайте до пятницы.";

        Assert.True(EvidenceCheck.Verify(transcript, "«Сделайте нормализацию до ТРЕТЬЕЙ формы»"));
        Assert.True(EvidenceCheck.Verify(transcript, "„сделайте нормализацию до третьей формы“, "));
        Assert.False(EvidenceCheck.Verify(transcript, string.Empty));
        Assert.False(EvidenceCheck.Verify(transcript, "   "));
        Assert.False(EvidenceCheck.Verify(transcript, "напишите эссе про индексы"));
    }

    /// <summary>
    /// Поля evidence, confidence и doubt читаются из ответа агента, а в старом JSON без них остаются пустыми
    /// </summary>
    [Fact]
    public void Items_parse_evidence_confidence_and_doubt()
    {
        var items = HomeworkItemParser.Parse("""
            [
              {"title": "Нормализация", "evidence": "сделайте нормализацию", "confidence": "low", "doubt": "срок не назван"},
              {"title": "Старое задание"}
            ]
            """);

        Assert.Equal(("сделайте нормализацию", "low", "срок не назван"), (items[0].Evidence, items[0].Confidence, items[0].Doubt));
        Assert.Equal((string.Empty, string.Empty, string.Empty), (items[1].Evidence, items[1].Confidence, items[1].Doubt));
    }

    /// <summary>
    /// Просьба к агенту требует цитату, уверенность и сомнение
    /// </summary>
    [Fact]
    public void The_extraction_prompt_asks_for_evidence_and_confidence()
    {
        var prompt = HomeworkExtractionPrompt.Build(Sample, "t.txt", "r.json");

        Assert.Contains("\"evidence\"", prompt);
        Assert.Contains("\"confidence\"", prompt);
        Assert.Contains("\"doubt\"", prompt);
    }

    /// <summary>
    /// Задание с названием и заданными уверенностью, цитатой и сомнением
    /// </summary>
    private static HomeworkItem Item(string confidence, string evidence = "сделайте нормализацию", string doubt = "") =>
        new("Нормализация", null, string.Empty, string.Empty, Evidence: evidence, Confidence: confidence, Doubt: doubt);

    /// <summary>
    /// Высокая уверенность и проверенная цитата: работа готовится сама
    /// </summary>
    [Fact]
    public void High_confidence_with_verified_evidence_is_ready()
    {
        var result = ItemAssessor.Assess(Item("high"), evidenceVerified: true, hasTranscript: true, hasCondition: true, confirmed: false);

        Assert.Equal(new ItemAssessment(ItemReadinessEnum.Ready, string.Empty), result);
    }

    /// <summary>
    /// Средняя уверенность: работа готовится, сомнение идёт пометкой
    /// </summary>
    [Fact]
    public void Medium_confidence_is_ready_with_the_doubt_as_note()
    {
        var result = ItemAssessor.Assess(Item("medium", doubt: "срок не назван"), true, true, true, false);

        Assert.Equal(new ItemAssessment(ItemReadinessEnum.ReadyWithNote, "срок не назван"), result);
    }

    /// <summary>
    /// Низкая уверенность, ненайденная цитата, нет расшифровки или условия: нужно подтверждение, вопрос называет причину
    /// </summary>
    [Fact]
    public void Low_confidence_missing_evidence_or_missing_inputs_need_confirmation_with_a_question()
    {
        var low = ItemAssessor.Assess(Item("low", doubt: "не тот предмет"), true, true, true, false);
        var noQuote = ItemAssessor.Assess(Item("high"), false, true, true, false);
        var noTranscript = ItemAssessor.Assess(Item("high"), true, false, true, false);
        var noCondition = ItemAssessor.Assess(Item("high"), true, true, false, false);

        Assert.All(new[] { low, noQuote, noTranscript, noCondition }, result =>
        {
            Assert.Equal(ItemReadinessEnum.NeedsConfirmation, result.Readiness);
            Assert.NotEmpty(result.Question);
        });
        Assert.Contains("не тот предмет", low.Question);
        Assert.Contains("цитату", noQuote.Question);
        Assert.Contains("расшифровк", noTranscript.Question);
        Assert.Contains("условие", noCondition.Question);
    }

    /// <summary>
    /// Подтверждённое владельцем задание готово, даже при низкой уверенности
    /// </summary>
    [Fact]
    public void A_confirmed_item_is_ready()
    {
        var result = ItemAssessor.Assess(Item("low"), false, false, false, confirmed: true);

        Assert.Equal(ItemReadinessEnum.Ready, result.Readiness);
    }

    /// <summary>
    /// Старое задание без новых полей (и с чужим значением уверенности без цитаты) подтверждения не требует
    /// </summary>
    [Fact]
    public void An_old_item_without_new_fields_is_ready()
    {
        var old = HomeworkItemParser.Parse("""[{"title": "Старое задание"}]""")[0];
        var unknown = Item("banana", evidence: string.Empty);

        Assert.Equal(ItemReadinessEnum.Ready, ItemAssessor.Assess(old, false, true, true, false).Readiness);
        Assert.Equal(ItemReadinessEnum.Ready, ItemAssessor.Assess(unknown, false, true, true, false).Readiness);
    }

    /// <summary>
    /// Неизвестная уверенность при цитате считается пустой: без найденной цитаты нужно подтверждение, с найденной готово
    /// </summary>
    [Fact]
    public void Unknown_confidence_with_evidence_is_judged_by_the_evidence()
    {
        Assert.Equal(ItemReadinessEnum.NeedsConfirmation, ItemAssessor.Assess(Item("banana"), false, true, true, false).Readiness);
        Assert.Equal(ItemReadinessEnum.Ready, ItemAssessor.Assess(Item("banana"), true, true, true, false).Readiness);
    }

    /// <summary>
    /// Подтверждение и правка условия сохраняются, ReadItems применяет правку, а без правок читает как раньше
    /// </summary>
    [Fact]
    public void Confirm_and_override_are_stored_and_applied_by_ReadItems()
    {
        var folder = Path.Combine(Path.GetTempPath(), "steward-pipeline-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new Steward.Homework.Storage.HomeworkStore(folder);
            Directory.CreateDirectory(Path.Combine(folder, "items"));
            File.WriteAllText(store.ItemsPath(Sample.Id), """[{"title": "Старое", "requirements": "старые"}, {"title": "Второе"}]""");

            Assert.Equal("Старое", store.ReadItems(Sample.Id)![0].Title);
            Assert.Empty(store.ConfirmedNumbers(Sample.Id));

            store.Confirm(Sample.Id, 2);
            store.SetOverride(Sample.Id, 1, "Верное", "верные требования");

            Assert.Equal([1, 2], store.ConfirmedNumbers(Sample.Id).Order());
            var items = store.ReadItems(Sample.Id)!;
            Assert.Equal(("Верное", "верные требования"), (items[0].Title, items[0].Requirements));
            Assert.Equal("Второе", items[1].Title);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// EvidenceVerified читает расшифровку и проверяет цитату задания
    /// </summary>
    [Fact]
    public void EvidenceVerified_checks_the_quote_against_the_transcript()
    {
        var folder = Path.Combine(Path.GetTempPath(), "steward-pipeline-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new Steward.Homework.Storage.HomeworkStore(folder);
            Directory.CreateDirectory(Path.Combine(folder, "items"));
            Directory.CreateDirectory(Path.Combine(folder, "transcripts"));
            File.WriteAllText(store.TranscriptPath(Sample.Id), "Сделайте нормализацию до третьей формы.");
            File.WriteAllText(store.ItemsPath(Sample.Id), """[{"title": "А", "evidence": "сделайте  нормализацию"}, {"title": "Б", "evidence": "напишите эссе"}, {"title": "В"}]""");

            Assert.True(store.EvidenceVerified(Sample, 1));
            Assert.False(store.EvidenceVerified(Sample, 2));
            Assert.False(store.EvidenceVerified(Sample, 3));
            Assert.False(store.EvidenceVerified(Sample, 9));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// Подтверждение переживает отметки «в архиве» и «сдано», а правка с пустыми полями оставляет прежние название и требования
    /// </summary>
    [Fact]
    public void Confirmation_survives_archive_and_submit_marks_and_empty_override_keeps_originals()
    {
        var folder = Path.Combine(Path.GetTempPath(), "steward-pipeline-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new Steward.Homework.Storage.HomeworkStore(folder);
            Directory.CreateDirectory(Path.Combine(folder, "items"));
            File.WriteAllText(store.ItemsPath(Sample.Id), """[{"title": "Старое", "requirements": "старые"}]""");

            store.Confirm(Sample.Id, 1);
            store.SetArchived(Sample.Id, 1, true);
            store.SetArchived(Sample.Id, 1, false);
            store.SetSubmitted(Sample.Id, 1, true);
            store.SetSubmitted(Sample.Id, 1, false);

            Assert.Equal([1], store.ConfirmedNumbers(Sample.Id).Order());

            store.SetOverride(Sample.Id, 1, string.Empty, string.Empty);
            var item = store.ReadItems(Sample.Id)![0];
            Assert.Equal(("Старое", "старые"), (item.Title, item.Requirements));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// Шаг берётся из файлов и отметок: сдано главнее всего, затем проверка, затем готовая работа
    /// </summary>
    [Theory]
    [InlineData(false, false, false, PipelineStepEnum.Found)]
    [InlineData(false, true, false, PipelineStepEnum.Ready)]
    [InlineData(false, false, true, PipelineStepEnum.Checked)]
    [InlineData(false, true, true, PipelineStepEnum.Checked)]
    [InlineData(true, false, false, PipelineStepEnum.Submitted)]
    [InlineData(true, true, false, PipelineStepEnum.Submitted)]
    [InlineData(true, false, true, PipelineStepEnum.Submitted)]
    [InlineData(true, true, true, PipelineStepEnum.Submitted)]
    public void The_step_follows_the_files_and_marks(bool submitted, bool hasDraft, bool hasCheck, PipelineStepEnum expected) =>
        Assert.Equal(expected, PipelineStepper.Of(submitted, hasDraft, hasCheck));

    /// <summary>
    /// Занятия с несданными заданиями идут первыми, без заданий следом, полностью сданные в конце; внутри группы по дате от свежих к старым
    /// </summary>
    [Fact]
    public void Lessons_with_pending_work_come_first_and_fully_submitted_go_last_by_date()
    {
        Assert.Equal(0, PipelineStepper.LessonGroup(3, 1));
        Assert.Equal(0, PipelineStepper.LessonGroup(2, 2));
        Assert.Equal(1, PipelineStepper.LessonGroup(0, 0));
        Assert.Equal(2, PipelineStepper.LessonGroup(2, 0));

        var lessons = new (string Id, DateOnly Date, int Items, int Pending)[]
        {
            ("a", new DateOnly(2026, 10, 25), 2, 0),
            ("b", new DateOnly(2026, 10, 10), 1, 1),
            ("c", new DateOnly(2026, 10, 20), 0, 0),
            ("d", new DateOnly(2026, 10, 15), 3, 2),
            ("e", new DateOnly(2026, 10, 12), 1, 0),
        };
        var order = lessons.OrderBy(item => PipelineStepper.LessonGroup(item.Items, item.Pending)).ThenByDescending(item => item.Date).Select(item => item.Id);

        Assert.Equal(["d", "b", "c", "a", "e"], order);
    }

    /// <summary>
    /// Временная папка домашних заданий с занятием и его заданиями
    /// </summary>
    /// <param name="itemsJson">Задания в JSON, как их пишет агент</param>
    /// <returns>Хранилище, занятие и папка, которую нужно удалить после теста</returns>
    private static (HomeworkStore Store, Lesson Lesson, string Folder) NewStore(string itemsJson)
    {
        var folder = Path.Combine(Path.GetTempPath(), "steward-card-" + Guid.NewGuid().ToString("N"));
        var store = new HomeworkStore(folder);
        store.SaveLessons([Sample]);
        Directory.CreateDirectory(Path.Combine(folder, "items"));
        File.WriteAllText(store.ItemsPath(Sample.Id), itemsJson);

        return (store, Sample, folder);
    }

    /// <summary>
    /// Кладёт файл в папку готовых работ занятия
    /// </summary>
    /// <param name="store">Хранилище</param>
    /// <param name="name">Имя файла</param>
    /// <returns>Путь к файлу</returns>
    private static string AddDraft(HomeworkStore store, string name)
    {
        Directory.CreateDirectory(store.DraftsDirectory(Sample.Id));
        var path = Path.Combine(store.DraftsDirectory(Sample.Id), name);
        File.WriteAllText(path, "работа");

        return path;
    }

    /// <summary>
    /// Удаляет временную папку теста
    /// </summary>
    /// <param name="folder">Папка</param>
    private static void Clean(string folder)
    {
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
    }

    /// <summary>
    /// Модель списка с одним занятием, задания и шаги которого читаются из хранилища
    /// </summary>
    /// <param name="store">Хранилище</param>
    /// <returns>Модель списка с прочитанными шагами</returns>
    private static async Task<HomeworkFormatsViewModel> ModelOver(HomeworkStore store)
    {
        var model = new HomeworkFormatsViewModel((_, _) => { }, _ => true, (_, _) => Task.FromResult((0, string.Empty)))
        {
            ItemsReader = id => store.ReadItems(id) is { } items
                ? [.. items.Select((item, index) => (index + 1, store.SubmittedNumbers(id).Contains(index + 1) ? item with { Done = true, Submitted = true } : item, false))]
                : null,
            PipelineReader = store.PipelineInfo,
            PathOpened = _ => { },
        };
        model.Update([(Sample, HomeworkFormatEnum.Word)]);
        await model.WhenPipelineLoaded();

        return model;
    }

    /// <summary>
    /// Агент-заглушка, который считает запуски: подтверждение и правка условия не должны его трогать
    /// </summary>
    private sealed class CountingAgent
    {
        /// <summary>
        /// Сколько раз агента запускали
        /// </summary>
        public int Runs { get; private set; }

        /// <summary>
        /// Запуск агента: только считает
        /// </summary>
        /// <param name="prompt">Просьба</param>
        /// <param name="workDirectory">Рабочая папка</param>
        /// <param name="extraDirectories">Дополнительные папки</param>
        /// <param name="cancellationToken">Отмена</param>
        /// <returns>Пустая строка</returns>
        public Task<string> Run(string prompt, string workDirectory, IReadOnlyList<string> extraDirectories, CancellationToken cancellationToken)
        {
            Runs++;

            return Task.FromResult(string.Empty);
        }
    }

    /// <summary>
    /// Шаг задания считается по его файлу: недописанные <c>.partial</c>, <c>.tmp</c> и <c>~$</c> не делают задание готовым, отчёт о проверке занятия не делает «Проверено» задание без работы, отметка «сдано» главнее всего
    /// </summary>
    [Fact]
    public void The_step_ignores_partial_and_lock_files_and_a_check_needs_a_work()
    {
        var (store, lesson, folder) = NewStore("""[{"title": "Первое"}, {"title": "Второе"}]""");
        try
        {
            AddDraft(store, "Задание 1. Отчёт.docx.partial");
            AddDraft(store, "~$Задание 1. Отчёт.docx");
            AddDraft(store, "Задание 1.tmp");

            Assert.Equal(PipelineStepEnum.Found, store.PipelineInfo(lesson)[1].Step);
            Assert.Equal(string.Empty, store.PipelineInfo(lesson)[1].WorkPath);

            var work = AddDraft(store, "Задание 1. Отчёт.docx");
            Assert.Equal(PipelineStepEnum.Ready, store.PipelineInfo(lesson)[1].Step);
            Assert.Equal(work, store.PipelineInfo(lesson)[1].WorkPath);

            Directory.CreateDirectory(Path.GetDirectoryName(store.CheckReportPath(lesson.Id))!);
            File.WriteAllText(store.CheckReportPath(lesson.Id), "отчёт");
            var info = store.PipelineInfo(lesson);
            Assert.Equal(PipelineStepEnum.Checked, info[1].Step);
            Assert.Equal(PipelineStepEnum.Found, info[2].Step);

            store.SetSubmitted(lesson.Id, 2, true);
            Assert.Equal(PipelineStepEnum.Submitted, store.PipelineInfo(lesson)[2].Step);
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// Задание без работы получает подсказку, что будет дальше; задание, на котором агент споткнулся три раза, говорит об ошибке
    /// </summary>
    [Fact]
    public void A_found_item_explains_what_happens_next()
    {
        var (store, lesson, folder) = NewStore("""[{"title": "Первое"}]""");
        try
        {
            store.SetSubjectState(lesson.Title, LessonStateEnum.Fetch);
            Assert.Contains("выключена", store.PipelineInfo(lesson)[1].Hint, StringComparison.Ordinal);

            store.SetSubjectAutoPrepare(lesson.Title, true);
            Assert.Contains("сама", store.PipelineInfo(lesson)[1].Hint, StringComparison.Ordinal);
            Assert.False(store.PipelineInfo(lesson)[1].HintIsProblem);

            File.WriteAllText(Path.Combine(store.Root, "prepare-attempts.json"), $$"""{"{{lesson.Id}}:1": {{HomeworkPreparer.MAX_ATTEMPTS}}}""");
            Assert.True(store.PipelineInfo(lesson)[1].HintIsProblem);
            Assert.Contains("не справился", store.PipelineInfo(lesson)[1].Hint, StringComparison.Ordinal);
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// Пайплайн на карточке: для одного задания на каждом шаге одна главная кнопка. Найдено: спокойная «Отметить сданным» (сдано мимо Steward), Готово и Проверено: «Посмотреть работу», а после просмотра «Отметить сданным», Сдано вручную: «Вернуть в работу», сдано в LMS: нет. Работу можно открыть из меню в любом шаге, где она есть, отметку «сдано» поставить или снять из меню в любом шаге
    /// </summary>
    [Fact]
    public async Task The_card_shows_one_main_button_per_step()
    {
        var items = new List<(int, HomeworkItem, bool)>
        {
            (1, new HomeworkItem("Найдено", null, string.Empty, string.Empty), false),
            (2, new HomeworkItem("Готово", null, string.Empty, string.Empty), false),
            (3, new HomeworkItem("Проверено", null, string.Empty, string.Empty), false),
            (4, new HomeworkItem("Сдано мной", null, string.Empty, string.Empty, true, Submitted: true), false),
            (5, new HomeworkItem("Сдано в LMS", null, string.Empty, string.Empty, true), false),
        };
        var ready = new ItemAssessment(ItemReadinessEnum.Ready, string.Empty);
        var info = new Dictionary<int, ItemPipelineInfo>
        {
            [1] = new(PipelineStepEnum.Found, string.Empty, ready, false, false, "Работа подготовится сама"),
            [2] = new(PipelineStepEnum.Ready, @"C:\w\Задание 2.docx", ready, false, false, string.Empty),
            [3] = new(PipelineStepEnum.Checked, @"C:\w\Задание 3.docx", ready, false, false, string.Empty),
            [4] = new(PipelineStepEnum.Submitted, @"C:\w\Задание 4.docx", ready, false, false, string.Empty),
            [5] = new(PipelineStepEnum.Submitted, string.Empty, ready, false, false, string.Empty),
        };
        var opened = new List<string>();
        var marks = new List<(int, bool)>();
        var model = new HomeworkFormatsViewModel([(Sample, HomeworkFormatEnum.Word)], (_, _) => { }, _ => true, (_, _) => Task.FromResult((0, string.Empty)))
        {
            ItemsReader = _ => items,
            PipelineReader = _ => info,
            PathOpened = opened.Add,
            ItemSubmitted = (_, number, submitted) => { lock (marks) marks.Add((number, submitted)); },
        };
        model.Update([(Sample, HomeworkFormatEnum.Word)]);
        await model.WhenPipelineLoaded();
        var lesson = model.Lessons.Single();

        Assert.Equal(
            [ItemMainActionEnum.MarkSubmitted, ItemMainActionEnum.ViewWork, ItemMainActionEnum.ViewWork, ItemMainActionEnum.ReturnToWork, ItemMainActionEnum.None],
            lesson.Items.Select(item => item.MainAction).ToArray());
        Assert.Equal(
            [(true, "Отметить сданным"), (true, "Посмотреть работу"), (true, "Посмотреть работу"), (true, "Вернуть в работу"), (false, string.Empty)],
            lesson.Items.Select(item => (item.HasMain, item.MainLabel)).ToArray());
        Assert.True(lesson.Items[0].MainIsQuiet);
        Assert.False(lesson.Items[1].MainIsQuiet);

        // Отметка «сдано» из меню есть в каждом шаге, кроме сданного по LMS
        Assert.Equal([true, true, true, true, false], lesson.Items.Select(item => item.ToggleSubmittedCommand.CanExecute(null)).ToArray());
        Assert.Equal("Отметить сданным", lesson.Items[0].SubmitLabel);
        Assert.Equal("Вернуть в работу", lesson.Items[3].SubmitLabel);

        // «Открыть работу» из меню: там, где файл есть, в том числе у сданного
        Assert.Equal([false, true, true, true, false], lesson.Items.Select(item => item.OpenWorkCommand.CanExecute(null)).ToArray());
        lesson.Items[3].OpenWorkCommand.Execute(null);
        Assert.Equal([@"C:\w\Задание 4.docx"], opened);
        opened.Clear();

        await model.MarkSubmittedAsync(lesson, 1, true);
        Assert.Equal([(1, true)], marks);
        Assert.Equal([PipelineStepEnum.Found, PipelineStepEnum.Ready, PipelineStepEnum.Checked, PipelineStepEnum.Submitted, PipelineStepEnum.Submitted], lesson.Items.Select(item => item.Step).ToArray());
        Assert.Equal(["Найдено", "Готово", "Проверено", "Сдано"], lesson.Items[2].Steps.Select(step => step.Title).ToArray());
        Assert.Equal([false, false, true, false], lesson.Items[2].Steps.Select(step => step.IsCurrent).ToArray());
        Assert.Equal("Работа подготовится сама", lesson.Items[0].Hint);

        lesson.Items[1].MainCommand.Execute(null);

        Assert.Equal([@"C:\w\Задание 2.docx"], opened);
        Assert.Equal("Отметить сданным", lesson.Items[1].MainLabel);
        Assert.Equal("Посмотреть работу", lesson.Items[2].MainLabel);

        // После просмотра работа по-прежнему открывается из меню
        lesson.Items[1].OpenWorkCommand.Execute(null);
        Assert.Equal([@"C:\w\Задание 2.docx", @"C:\w\Задание 2.docx"], opened);

        // Меню «…»: действия с готовой работой доступны только там, где она есть, указать сданный файл можно только у сданного
        Assert.False(lesson.Items[0].RecheckCommand.CanExecute(null));
        Assert.True(lesson.Items[1].RecheckCommand.CanExecute(null));
        Assert.False(lesson.Items[1].OpenCheckCommand.CanExecute(null));
        Assert.True(lesson.Items[2].OpenCheckCommand.CanExecute(null));
        Assert.False(lesson.Items[1].SubmittedFileCommand.CanExecute(null));
        Assert.True(lesson.Items[3].SubmittedFileCommand.CanExecute(null));
        Assert.True(lesson.Items[0].AttachWorkCommand.CanExecute(null));
    }

    /// <summary>
    /// Задание с низкой уверенностью на шаге «Найдено» показывает вопрос и три кнопки, а задание со средней уверенностью только пометку «проверь формулировку»
    /// </summary>
    [Fact]
    public async Task A_low_confidence_item_shows_the_question_and_its_buttons()
    {
        var items = new List<(int, HomeworkItem, bool)>
        {
            (1, new HomeworkItem("Эссе", null, string.Empty, string.Empty, Confidence: "low", Doubt: "не тот предмет"), false),
            (2, new HomeworkItem("Отчёт", null, string.Empty, string.Empty, Confidence: "medium", Doubt: "срок не назван"), false),
        };
        var info = new Dictionary<int, ItemPipelineInfo>
        {
            [1] = new(PipelineStepEnum.Found, string.Empty, new ItemAssessment(ItemReadinessEnum.NeedsConfirmation, "Не уверен, что правильно понял задание «Эссе» (не тот предмет)"), false, false, string.Empty),
            [2] = new(PipelineStepEnum.Ready, @"C:\w\Задание 2.docx", new ItemAssessment(ItemReadinessEnum.ReadyWithNote, "срок не назван"), true, true, string.Empty),
        };
        var edits = new List<int>();
        var model = new HomeworkFormatsViewModel([(Sample, HomeworkFormatEnum.Word)], (_, _) => { }, _ => true, (_, _) => Task.FromResult((0, string.Empty)))
        {
            ItemsReader = _ => items,
            PipelineReader = _ => info,
            ItemConfirmer = (_, _) => { },
        };
        model.ConditionEditRequested += (_, item) => edits.Add(item.Number);
        model.Update([(Sample, HomeworkFormatEnum.Word)]);
        await model.WhenPipelineLoaded();
        var question = model.Lessons.Single().Items[0];
        var note = model.Lessons.Single().Items[1];

        Assert.True(question.NeedsConfirmation);
        Assert.Contains("не тот предмет", question.Question, StringComparison.Ordinal);
        Assert.True(question.ConfirmCommand.CanExecute(null));
        Assert.True(question.FixConditionCommand.CanExecute(null));
        Assert.True(question.ArchiveCommand.CanExecute(null));
        Assert.Equal(ItemMainActionEnum.MarkSubmitted, question.MainAction);
        Assert.True(question.MainIsQuiet);
        Assert.False(question.HasNote);

        question.FixConditionCommand.Execute(null);
        Assert.Equal([1], edits);

        Assert.False(note.NeedsConfirmation);
        Assert.True(note.HasNote);
        Assert.Equal("Проверь формулировку: срок не назван", note.Note);
    }

    /// <summary>
    /// «Подтвердить» записывает подтверждение в фоне, вопрос пропадает, а задание ждёт автоподготовки
    /// </summary>
    [Fact]
    public async Task Confirm_moves_the_item_on_and_the_question_disappears()
    {
        var (store, lesson, folder) = NewStore("""[{"title": "Эссе", "confidence": "low", "doubt": "не тот предмет", "evidence": "напишите эссе"}]""");
        try
        {
            store.SetSubjectState(lesson.Title, LessonStateEnum.Fetch);
            store.SetSubjectAutoPrepare(lesson.Title, true);
            var agent = new CountingAgent();
            var preparer = new HomeworkPreparer(store, new GuideStore(folder), new RemarkStore(Path.Combine(folder, "remarks")), agent.Run);
            var model = await ModelOver(store);
            model.ItemConfirmer = (_, number) => preparer.Confirm(lesson, number);
            var row = model.Lessons.Single();

            Assert.True(row.Items[0].NeedsConfirmation);

            await model.ConfirmAsync(row, 1);

            Assert.Equal(0, agent.Runs);
            Assert.Equal([1], store.ConfirmedNumbers(lesson.Id).ToArray());
            Assert.False(row.Items[0].NeedsConfirmation);
            Assert.Equal(PipelineStepEnum.Found, row.Items[0].Step);
            Assert.Contains("сама", row.Items[0].Hint, StringComparison.Ordinal);
            Assert.False(row.Failed);
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// «Поправить условие» записывает правку и замечание вида «условие» для этого предмета, агента не запускает; без изменений замечание не пишется; сбой записи правки показывается ошибкой
    /// </summary>
    [Fact]
    public async Task Fixing_the_condition_saves_the_override_and_a_condition_remark()
    {
        var (store, lesson, folder) = NewStore("""[{"title": "Эссе", "confidence": "low"}]""");
        try
        {
            var guides = new GuideStore(folder);
            var remarks = new RemarkStore(Path.Combine(folder, "remarks"));
            var agent = new CountingAgent();
            var preparer = new HomeworkPreparer(store, guides, remarks, agent.Run);
            var model = await ModelOver(store);
            model.ConditionFixer = (_, number, title, requirements) => preparer.FixCondition(lesson, number, title, requirements);
            var row = model.Lessons.Single();

            await model.FixConditionAsync(row, 1, "Эссе по истории игр", "две страницы");

            Assert.Equal(0, agent.Runs);
            Assert.Equal(("Эссе по истории игр", "две страницы"), (store.ReadItems(lesson.Id)![0].Title, store.ReadItems(lesson.Id)![0].Requirements));
            Assert.Equal([1], store.ConfirmedNumbers(lesson.Id).ToArray());
            var remark = Assert.Single(remarks.Load(lesson.Title));
            Assert.Equal((RemarkKindEnum.Condition, false, 1), (remark.Kind, remark.Global, remark.Number));
            Assert.Contains("Эссе по истории игр", remark.Text, StringComparison.Ordinal);
            Assert.Equal("Эссе по истории игр", row.Items[0].Title);
            Assert.False(row.Items[0].NeedsConfirmation);

            // То же условие ещё раз: правка не новая, второго замечания нет
            await model.FixConditionAsync(row, 1, "Эссе по истории игр", "две страницы");
            Assert.Single(remarks.Load(lesson.Title));
            Assert.False(row.Failed);

            model.ConditionFixer = (_, _, _, _) => throw new IOException("файл занят");
            await model.FixConditionAsync(row, 1, "Другое", string.Empty);
            Assert.True(row.Failed);
            Assert.Contains("файл занят", row.Status, StringComparison.Ordinal);
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// «Отметить сданным» ставит отметку и в фоне делает файл работы образцом предмета; шаг становится «Сдано»
    /// </summary>
    [Fact]
    public async Task Marking_submitted_registers_the_draft_as_a_sample()
    {
        var (store, lesson, folder) = NewStore("""[{"title": "Отчёт"}]""");
        try
        {
            var guides = new GuideStore(folder);
            var preparer = new HomeworkPreparer(store, guides, new RemarkStore(Path.Combine(folder, "remarks")), (_, _, _, _) => Task.FromResult("агент не нужен"));
            var work = AddDraft(store, "Задание 1. Отчёт.docx");
            var model = await ModelOver(store);
            model.ItemSubmitted = store.SetSubmitted;
            model.SampleRegistrar = (_, number) => preparer.RegisterSubmittedSample(lesson, number);
            var row = model.Lessons.Single();

            row.Items[0].MainCommand.Execute(null);
            Assert.Equal("Отметить сданным", row.Items[0].MainLabel);

            await model.MarkSubmittedAsync(row, 1, true);

            Assert.Equal([1], store.SubmittedNumbers(lesson.Id).ToArray());
            var sample = Assert.Single(guides.Load(), entry => entry.Kind == GuideKindEnum.Example);
            Assert.Equal(($"sample:{lesson.Id}:1", false), (sample.Id, sample.Manual));
            Assert.Equal(File.ReadAllText(work), File.ReadAllText(sample.Path));
            Assert.Equal(PipelineStepEnum.Submitted, row.Items[0].Step);
            Assert.Equal("Вернуть в работу", row.Items[0].MainLabel);
            Assert.Contains("стала образцом", row.Status, StringComparison.Ordinal);
            Assert.False(row.Failed);

            // «Вернуть в работу», работу переделали, сдали снова: образец заменяется новой версией, хотя учёт уже знает задание
            await model.MarkSubmittedAsync(row, 1, false);
            File.WriteAllText(work, "переделанная работа");
            await model.MarkSubmittedAsync(row, 1, true);
            sample = Assert.Single(guides.Load(), entry => entry.Kind == GuideKindEnum.Example);
            Assert.Equal("переделанная работа", File.ReadAllText(sample.Path));

            // Файл владельца черновиком не заменяется, и сообщение говорит правду
            var mine = Path.Combine(folder, "моя.docx");
            File.WriteAllText(mine, "моя версия");
            Assert.Equal(string.Empty, preparer.RegisterSubmittedFile(lesson, 1, mine));
            await model.MarkSubmittedAsync(row, 1, true);
            Assert.Equal("моя версия", File.ReadAllText(Assert.Single(guides.Load(), entry => entry.Kind == GuideKindEnum.Example).Path));
            Assert.Contains("указанный тобой файл", row.Status, StringComparison.Ordinal);
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// «Указать файл сданной работы» делает выбранный файл образцом владельца вместо черновика, а сбой записи показывается в карточке
    /// </summary>
    [Fact]
    public async Task The_submitted_file_replaces_the_sample_as_the_owners()
    {
        var (store, lesson, folder) = NewStore("""[{"title": "Отчёт"}]""");
        try
        {
            var guides = new GuideStore(folder);
            var preparer = new HomeworkPreparer(store, guides, new RemarkStore(Path.Combine(folder, "remarks")), (_, _, _, _) => Task.FromResult("агент не нужен"));
            AddDraft(store, "Задание 1. Отчёт.docx");
            store.SetSubmitted(lesson.Id, 1, true);
            Assert.True(preparer.RegisterSubmittedSample(lesson, 1).Registered);
            var mine = Path.Combine(folder, "моя версия.docx");
            File.WriteAllText(mine, "моя сданная версия");
            var model = await ModelOver(store);
            model.SubmittedFileSetter = (_, number, file) => preparer.RegisterSubmittedFile(lesson, number, file);
            var row = model.Lessons.Single();

            await model.SetSubmittedFileAsync(row, 1, mine);

            var sample = Assert.Single(guides.Load(), entry => entry.Kind == GuideKindEnum.Example);
            Assert.True(sample.Manual);
            Assert.Equal("моя сданная версия", File.ReadAllText(sample.Path));
            Assert.False(row.Failed);

            await model.SetSubmittedFileAsync(row, 1, Path.Combine(folder, "нет такого.docx"));
            Assert.True(row.Failed);

            // Файл больше предела образцом не становится
            var huge = Path.Combine(folder, "огромный.docx");
            using (var stream = new FileStream(huge, FileMode.Create))
                stream.SetLength(HomeworkStore.OWN_WORK_MAX_BYTES + 1);
            Assert.Contains("МБ", preparer.RegisterSubmittedFile(lesson, 1, huge), StringComparison.Ordinal);
            Assert.Equal("моя сданная версия", File.ReadAllText(Assert.Single(guides.Load(), entry => entry.Kind == GuideKindEnum.Example).Path));
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// Блок «Откуда это задание»: значок надёжности по проверенной цитате и работе LMS. Ненайденная цитата предупреждает, старое задание без цитаты и LMS честно так и названо
    /// </summary>
    [Fact]
    public void The_source_block_names_how_reliable_the_item_is()
    {
        var ready = new ItemAssessment(ItemReadinessEnum.Ready, string.Empty);
        LessonItemViewModel Row(string evidence, bool verified, bool lms) =>
            new(1, new HomeworkItem("А", null, string.Empty, string.Empty, Evidence: evidence), false, _ => { }, _ => { }, pipeline: new ItemPipelineInfo(PipelineStepEnum.Found, string.Empty, ready, verified, lms, string.Empty));

        Assert.Equal(("подтверждено цитатой и LMS", false), (Row("сделайте", true, true).Reliability, Row("сделайте", true, true).ReliabilityWarning));
        Assert.Equal(("только расшифровка", false), (Row("сделайте", true, false).Reliability, Row("сделайте", true, false).ReliabilityWarning));
        Assert.Equal(("только LMS", false), (Row(string.Empty, false, true).Reliability, Row(string.Empty, false, true).ReliabilityWarning));
        Assert.Equal(("цитата не найдена", true), (Row("сделайте", false, true).Reliability, Row("сделайте", false, true).ReliabilityWarning));
        Assert.Equal(("цитата не найдена", true), (Row("сделайте", false, false).Reliability, Row("сделайте", false, false).ReliabilityWarning));
        Assert.Equal(("расшифровка без цитаты", false), (Row(string.Empty, false, false).Reliability, Row(string.Empty, false, false).ReliabilityWarning));
        Assert.True(Row("сделайте", true, false).HasEvidence);
        Assert.Equal("«сделайте»", Row("сделайте", true, false).Quote);
    }

    /// <summary>
    /// Пока шаги не прочитаны (первый показ или ошибка чтения), карточка нейтральная: без значка «цитата не найдена» и без выделенного шага
    /// </summary>
    [Fact]
    public void Before_the_pipeline_is_read_the_card_is_neutral()
    {
        var row = new LessonItemViewModel(1, new HomeworkItem("А", null, string.Empty, string.Empty, Evidence: "сделайте"), false, _ => { }, _ => { });

        Assert.False(row.PipelineKnown);
        Assert.Equal((string.Empty, false, false), (row.Reliability, row.ReliabilityWarning, row.HasReliability));
        Assert.DoesNotContain(row.Steps, step => step.IsCurrent || step.IsPassed);
        Assert.Equal(string.Empty, row.Hint);
    }

    /// <summary>
    /// Задания читаются не в потоке окна и один раз на обновление: обновление списка не ждёт чтения, а результат приходит вместе с шагами
    /// </summary>
    [Fact]
    public async Task Items_are_read_off_the_ui_thread_once_per_refresh()
    {
        using var gate = new ManualResetEventSlim();
        var calls = 0;
        var released = new List<bool>();
        var contexts = new List<SynchronizationContext?>();
        var model = new HomeworkFormatsViewModel((_, _) => { }, _ => true, (_, _) => Task.FromResult((0, string.Empty)))
        {
            ItemsReader = _ =>
            {
                Interlocked.Increment(ref calls);
                lock (released)
                {
                    released.Add(gate.Wait(TimeSpan.FromSeconds(5)));
                    contexts.Add(SynchronizationContext.Current);
                }

                return [(1, new HomeworkItem("А", null, string.Empty, string.Empty), false)];
            },
            PipelineReader = _ => new Dictionary<int, ItemPipelineInfo> { [1] = new(PipelineStepEnum.Found, string.Empty, new ItemAssessment(ItemReadinessEnum.Ready, string.Empty), false, false, string.Empty) },
        };

        // Поток окна изображает контекст синхронизации: чтение не должно идти в нём
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
        try
        {
            model.Update([(Sample, HomeworkFormatEnum.Word)]);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        Assert.Empty(model.Lessons.Single().Items);
        gate.Set();
        await model.WhenPipelineLoaded();

        Assert.Equal(1, calls);
        Assert.All(released, Assert.True);
        Assert.All(contexts, Assert.Null);
        var item = Assert.Single(model.Lessons.Single().Items);
        Assert.True(item.PipelineKnown);
    }

    /// <summary>
    /// Ошибка чтения заданий заканчивается сообщением в карточке занятия, а не потерянным исключением
    /// </summary>
    [Fact]
    public async Task An_items_reader_failure_ends_as_a_message()
    {
        var unobserved = 0;
        void Count(object? sender, UnobservedTaskExceptionEventArgs e) => Interlocked.Increment(ref unobserved);
        TaskScheduler.UnobservedTaskException += Count;
        try
        {
            var broken = true;
            var model = new HomeworkFormatsViewModel((_, _) => { }, _ => true, (_, _) => Task.FromResult((0, string.Empty)))
            {
                ItemsReader = _ => broken ? throw new InvalidOperationException("файл заданий сломан") : [(1, new HomeworkItem("А", null, string.Empty, string.Empty), false)],
            };
            model.Update([(Sample, HomeworkFormatEnum.Word)]);
            await model.WhenPipelineLoaded();

            var row = model.Lessons.Single();
            Assert.True(row.Failed);
            Assert.Contains("файл заданий сломан", row.Status, StringComparison.Ordinal);

            // Удачное чтение убирает сообщение об ошибке
            broken = false;
            model.Update([(Sample, HomeworkFormatEnum.Word)]);
            await model.WhenPipelineLoaded();
            Assert.False(row.Failed);
            Assert.Equal(string.Empty, row.Status);
            Assert.Single(row.Items);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            Assert.Equal(0, unobserved);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Count;
        }
    }

    /// <summary>
    /// Ошибка чтения шагов не теряет задания: они показываются в нейтральной карточке
    /// </summary>
    [Fact]
    public async Task A_pipeline_failure_keeps_the_items_neutral()
    {
        var model = new HomeworkFormatsViewModel((_, _) => { }, _ => true, (_, _) => Task.FromResult((0, string.Empty)))
        {
            ItemsReader = _ => [(1, new HomeworkItem("А", null, string.Empty, string.Empty, Evidence: "сделайте"), false)],
            PipelineReader = _ => throw new IOException("файл занят"),
        };
        model.Update([(Sample, HomeworkFormatEnum.Word)]);
        await model.WhenPipelineLoaded();

        var item = Assert.Single(model.Lessons.Single().Items);
        Assert.False(item.PipelineKnown);
        Assert.False(item.HasReliability);
    }

    /// <summary>
    /// Действие владельца не переставляет занятия под курсором: занятие уходит вниз только при следующем обновлении списка
    /// </summary>
    [Fact]
    public async Task A_user_action_does_not_reorder_but_Update_does()
    {
        var fresh = new Lesson("fresh", "Физика", new DateOnly(2026, 10, 25));
        var old = new Lesson("old", "Физика", new DateOnly(2026, 10, 10));
        var submitted = new HashSet<string>();
        var model = new HomeworkFormatsViewModel((_, _) => { }, _ => true, (_, _) => Task.FromResult((0, string.Empty)))
        {
            ItemsReader = id =>
            {
                bool done;
                lock (submitted)
                    done = submitted.Contains(id);

                return [(1, new HomeworkItem("Задание", null, string.Empty, string.Empty, done, Submitted: done), false)];
            },
            ItemSubmitted = (id, _, value) =>
            {
                lock (submitted)
                {
                    if (value)
                        submitted.Add(id);
                    else
                        submitted.Remove(id);
                }
            },
        };
        IReadOnlyList<(Lesson, HomeworkFormatEnum)> all = [(fresh, HomeworkFormatEnum.Word), (old, HomeworkFormatEnum.Word)];
        model.Update(all);
        await model.WhenPipelineLoaded();
        Assert.Equal(["fresh", "old"], model.Lessons.Select(row => row.Id).ToArray());

        await model.MarkSubmittedAsync(model.Lessons[0], 1, true);
        await model.WhenPipelineLoaded();

        Assert.True(model.Lessons[0].AllSubmitted);
        Assert.Equal(["fresh", "old"], model.Lessons.Select(row => row.Id).ToArray());

        model.Update(all);
        await model.WhenPipelineLoaded();
        Assert.Equal(["old", "fresh"], model.Lessons.Select(row => row.Id).ToArray());
    }

    /// <summary>
    /// Работой LMS считается только цифровой номер работы, как в оценке уверенности: ссылка в тексте задания значок «LMS» не даёт
    /// </summary>
    [Fact]
    public void Only_a_numeric_lms_work_counts_as_lms()
    {
        var (store, lesson, folder) = NewStore("""[{"title": "А", "activity": "4211"}, {"title": "Б", "link": "https://example.com/task"}, {"title": "В", "activity": "abc"}]""");
        try
        {
            var info = store.PipelineInfo(lesson);

            Assert.Equal([true, false, false], new[] { info[1].LinkedToLms, info[2].LinkedToLms, info[3].LinkedToLms });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// Отметки владельца из нескольких потоков не затирают друг друга: чтение, изменение и запись идут под общей блокировкой с уникальными временными файлами
    /// </summary>
    [Fact]
    public async Task Parallel_marks_are_not_lost()
    {
        var (store, lesson, folder) = NewStore("""[{"title": "А"}]""");
        try
        {
            await Task.WhenAll(Enumerable.Range(1, 24).Select(number => Task.Run(() =>
            {
                if (number % 3 == 0)
                    store.Confirm(lesson.Id, number);
                else if (number % 3 == 1)
                    store.SetSubmitted(lesson.Id, number, true);
                else
                    store.SetOverride(lesson.Id, number, $"Задание {number}", string.Empty);
            })));

            Assert.Equal(8, store.SubmittedNumbers(lesson.Id).Count);
            Assert.Equal(16, store.ConfirmedNumbers(lesson.Id).Count);
            Assert.Empty(Directory.EnumerateFiles(folder, "*.tmp"));
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// Занятия в списке идут по заданиям: с несданными сверху, без заданий ниже, всё сданное внизу, внутри группы от свежих к старым; когда задание отметили сданным, строки заданий заменяются и занятие уходит вниз при следующем обновлении
    /// </summary>
    [Fact]
    public async Task Update_orders_lessons_and_SetItems_follows_the_submitted_mark()
    {
        var done = new Lesson("done", "Физика", new DateOnly(2026, 10, 25));
        var pending = new Lesson("pending", "Физика", new DateOnly(2026, 10, 10));
        var empty = new Lesson("empty", "Физика", new DateOnly(2026, 10, 20));
        var submitted = false;
        var model = new HomeworkFormatsViewModel([(done, HomeworkFormatEnum.Word), (pending, HomeworkFormatEnum.Word), (empty, HomeworkFormatEnum.Word)], (_, _) => { }, _ => true, (_, _) => Task.FromResult((0, string.Empty)))
        {
            ItemsReader = id => id switch
            {
                "done" => [(1, new HomeworkItem("Сдано", null, string.Empty, string.Empty, true), false)],
                "pending" => [(1, new HomeworkItem("Ждёт", null, string.Empty, string.Empty, submitted, Submitted: submitted), false)],
                _ => [],
            },
        };
        IReadOnlyList<(Lesson, HomeworkFormatEnum)> all = [(done, HomeworkFormatEnum.Word), (pending, HomeworkFormatEnum.Word), (empty, HomeworkFormatEnum.Word)];
        model.Update(all);
        await model.WhenPipelineLoaded();

        Assert.Equal(["pending", "empty", "done"], model.Lessons.Select(row => row.Id).ToArray());

        var row = model.Lessons[0];
        var before = row.Items;
        submitted = true;
        model.Update(all);
        await model.WhenPipelineLoaded();

        Assert.NotSame(before, row.Items);
        Assert.True(row.Items[0].Submitted);
        Assert.True(row.AllSubmitted);
        Assert.Equal(["empty", "done", "pending"], model.Lessons.Select(lesson => lesson.Id).ToArray());
    }

    /// <summary>
    /// Отказ удалить работу из-за идущей фоновой подготовки показывается в карточке как есть, без совета закрыть файл в Word
    /// </summary>
    [Fact]
    public void A_busy_refusal_to_delete_drafts_is_shown_as_is()
    {
        var lesson = new Lesson("l1", "Физика", new DateOnly(2026, 10, 10));
        var model = new HomeworkFormatsViewModel([(lesson, HomeworkFormatEnum.Word)], (_, _) => { }, _ => true, (_, _) => Task.FromResult((0, string.Empty)))
        {
            DraftsDeleted = _ => HomeworkPreparer.DELETE_BUSY_MESSAGE,
        };

        model.DeleteDrafts(model.Lessons[0]);

        Assert.Equal(HomeworkPreparer.DELETE_BUSY_MESSAGE, model.Lessons[0].Status);
        Assert.True(model.Lessons[0].Failed);
    }

    /// <summary>
    /// «В архив» при занятом файле отметок не роняет окно: причина показывается в карточке
    /// </summary>
    [Fact]
    public async Task A_failed_archive_is_shown_in_the_card()
    {
        var lesson = new Lesson("l1", "Физика", new DateOnly(2026, 10, 10));
        var model = new HomeworkFormatsViewModel([(lesson, HomeworkFormatEnum.Word)], (_, _) => { }, _ => true, (_, _) => Task.FromResult((0, string.Empty)))
        {
            ItemsReader = _ => [(1, new HomeworkItem("Эссе", null, string.Empty, string.Empty, false), false)],
            ItemArchived = (_, _) => throw new IOException("файл занят"),
        };
        model.Update([(lesson, HomeworkFormatEnum.Word)]);
        await model.WhenPipelineLoaded();

        model.Lessons[0].Items[0].ArchiveCommand.Execute(null);

        Assert.True(model.Lessons[0].Failed);
        Assert.Contains("файл занят", model.Lessons[0].Status, StringComparison.Ordinal);
    }

    /// <summary>
    /// Сборка строк из прежнего снимка не перекрывает свежий снимок: после многих обновлений подряд строки всегда показывают последнее прочитанное
    /// </summary>
    [Fact]
    public async Task A_stale_cached_build_never_overrides_a_fresh_snapshot()
    {
        var lesson = new Lesson("pending", "Физика", new DateOnly(2026, 10, 10));
        var submitted = false;
        var model = new HomeworkFormatsViewModel([(lesson, HomeworkFormatEnum.Word)], (_, _) => { }, _ => true, (_, _) => Task.FromResult((0, string.Empty)))
        {
            ItemsReader = _ => [(1, new HomeworkItem("Ждёт", null, string.Empty, string.Empty, Volatile.Read(ref submitted), Submitted: Volatile.Read(ref submitted)), false)],
        };
        IReadOnlyList<(Lesson, HomeworkFormatEnum)> all = [(lesson, HomeworkFormatEnum.Word)];
        model.Update(all);
        await model.WhenPipelineLoaded();

        for (var round = 0; round < 200; round++)
        {
            Volatile.Write(ref submitted, round % 2 == 0);
            model.Update(all);
            await model.WhenPipelineLoaded();

            Assert.Equal(round % 2 == 0, model.Lessons[0].Items[0].Submitted);
        }
    }

    /// <summary>
    /// «Проверить заново» не запускает агента, пока фоновая подготовка держит блокировку
    /// </summary>
    [Fact]
    public async Task A_recheck_is_refused_while_a_background_prepare_runs()
    {
        var (store, lesson, folder) = NewStore("""[{"title": "Отчёт"}]""");
        try
        {
            AddDraft(store, "Задание 1. Отчёт.docx");
            var runs = 0;
            var preparer = new HomeworkPreparer(store, new GuideStore(folder), new RemarkStore(Path.Combine(folder, "remarks")), (_, _, _, _) =>
            {
                runs++;

                return Task.FromResult(string.Empty);
            });

            string error;
            using (new FileStream(Path.Combine(folder, "prepare.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                error = await preparer.CheckNowAsync(lesson, CancellationToken.None);

            Assert.Equal(0, runs);
            Assert.Contains("в фоне", error, StringComparison.Ordinal);
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// Замечание из окна «Переделать» запоминается с выбранными областью и видом: без «Для всех предметов» в файл предмета, с ним в <c>_all.json</c>; повтор не дублируется; переделка идёт и тогда, когда замечание записать не удалось, а владелец видит предупреждение
    /// </summary>
    [Fact]
    public async Task The_redo_remark_is_saved_with_the_chosen_scope_and_kind()
    {
        var (store, lesson, folder) = NewStore("""[{"title": "Отчёт"}]""");
        try
        {
            var remarksFolder = Path.Combine(folder, "remarks");
            var remarks = new RemarkStore(remarksFolder);
            var preparer = new HomeworkPreparer(store, new GuideStore(folder), remarks, new CountingAgent().Run);
            var requests = new List<PrepareRequest>();
            var model = new HomeworkFormatsViewModel((_, _) => { }, _ => true, (_, request) =>
            {
                requests.Add(request);

                return Task.FromResult((1, string.Empty));
            })
            {
                RemarkSaver = (target, text, kind, global) => preparer.SaveRedoRemark(target, text, kind, global),
            };
            model.Update([(lesson, HomeworkFormatEnum.Word)]);
            await model.WhenPipelineLoaded();
            var row = model.Lessons.Single();

            // Только этот предмет
            await model.RedoAsync(row, HomeworkFormatEnum.Word, "Слишком много воды", [], RemarkKindEnum.Content, global: false);
            var own = Assert.Single(remarks.Load(lesson.Title));
            Assert.Equal(("Слишком много воды", RemarkKindEnum.Content, false, lesson.Id, 0), (own.Text, own.Kind, own.Global, own.LessonId, own.Number));
            Assert.False(File.Exists(Path.Combine(remarksFolder, "_all.json")));
            var request = Assert.Single(requests);
            Assert.True(request.Redo);
            Assert.Equal("Слишком много воды", request.Extra);

            // Повтор того же текста не дублируется
            await model.RedoAsync(row, HomeworkFormatEnum.Word, "  слишком   много воды ", [], RemarkKindEnum.Content, global: false);
            Assert.Single(remarks.Load(lesson.Title));

            // Для всех предметов: в общем файле, не в файле предмета
            await model.RedoAsync(row, HomeworkFormatEnum.Word, "Без канцелярита", [], RemarkKindEnum.Style, global: true);
            Assert.True(File.Exists(Path.Combine(remarksFolder, "_all.json")));
            Assert.Single(remarks.Load(lesson.Title));
            var shared = Assert.Single(remarks.LoadAll(), remark => remark.Global);
            Assert.Equal(("Без канцелярита", RemarkKindEnum.Style), (shared.Text, shared.Kind));

            // Только файлы: замечания нет, переделка идёт
            await model.RedoAsync(row, HomeworkFormatEnum.Word, string.Empty, [@"C:\замечания.docx"], RemarkKindEnum.General, global: false);
            Assert.Equal(2, remarks.LoadAll().Count);
            Assert.Equal(4, requests.Count);

            // Файл предмета занят: переделка всё равно идёт, владелец видит предупреждение
            var subjectFile = Path.Combine(remarksFolder, RemarkStore.SafeName(lesson.Title) + ".json");
            using (new FileStream(subjectFile, FileMode.Open, FileAccess.Read, FileShare.None))
                await model.RedoAsync(row, HomeworkFormatEnum.Word, "Добавить таблицу", [], RemarkKindEnum.Format, global: false);

            Assert.Equal(5, requests.Count);
            Assert.Contains("Замечание не запомнилось", row.Status, StringComparison.Ordinal);
            Assert.DoesNotContain(remarks.Load(lesson.Title), remark => remark.Text == "Добавить таблицу");
        }
        finally
        {
            Clean(folder);
        }
    }
}
