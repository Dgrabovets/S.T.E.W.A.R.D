using Steward.Homework.Models;
using Steward.Homework.Services;

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
}
