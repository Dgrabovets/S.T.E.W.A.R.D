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
}
