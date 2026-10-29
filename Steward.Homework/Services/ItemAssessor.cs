using Steward.Homework.Models;

namespace Steward.Homework.Services;

/// <summary>
/// Готовность задания к подготовке работы
/// </summary>
public enum ItemReadinessEnum
{
    /// <summary>
    /// Работа готовится сама
    /// </summary>
    Ready,

    /// <summary>
    /// Работа готовится, но на карточке пометка «проверь формулировку»
    /// </summary>
    ReadyWithNote,

    /// <summary>
    /// Задание ждёт подтверждения владельца и само не готовится
    /// </summary>
    NeedsConfirmation,
}

/// <summary>
/// Оценка задания: готовность и вопрос или пометка для карточки
/// </summary>
/// <param name="Readiness">Готовность</param>
/// <param name="Question">Вопрос владельцу (для «Нужно подтвердить») или сомнение (для «С пометкой»); для «Готово» пусто</param>
public sealed record ItemAssessment(ItemReadinessEnum Readiness, string Question);

/// <summary>
/// Решает, можно ли готовить работу по заданию само, по уверенности агента и проверкам
/// </summary>
public static class ItemAssessor
{
    /// <summary>
    /// Оценивает задание по правилам раздела «Уверенность и подтверждение» спецификации
    /// </summary>
    /// <param name="item">Задание</param>
    /// <param name="evidenceVerified">Цитата найдена в расшифровке</param>
    /// <param name="hasTranscript">Расшифровка занятия есть</param>
    /// <param name="hasCondition">Условие задания есть</param>
    /// <param name="confirmed">Владелец подтвердил задание</param>
    /// <returns>Готовность и вопрос</returns>
    public static ItemAssessment Assess(HomeworkItem item, bool evidenceVerified, bool hasTranscript, bool hasCondition, bool confirmed)
    {
        if (confirmed)
            return new ItemAssessment(ItemReadinessEnum.Ready, string.Empty);

        var confidence = Normalize(item.Confidence);

        // Старое задание без новых полей: подтверждение не требуется
        if (confidence.Length == 0 && item.Evidence.Trim().Length == 0)
            return new ItemAssessment(ItemReadinessEnum.Ready, string.Empty);

        var doubt = item.Doubt.Trim();
        var reason = doubt.Length > 0 ? $" ({doubt})" : string.Empty;

        if (confidence == "low")
            return Ask($"Не уверен, что правильно понял задание «{item.Title}»{reason}");

        if (!hasTranscript)
            return Ask($"Нет расшифровки занятия, не могу проверить задание «{item.Title}»");

        if (!hasCondition)
            return Ask($"Не выгружено условие задания «{item.Title}»");

        if (!evidenceVerified)
            return Ask($"Не нашёл цитату задания «{item.Title}» в расшифровке");

        return confidence == "medium"
            ? new ItemAssessment(ItemReadinessEnum.ReadyWithNote, doubt.Length > 0 ? doubt : "Проверь формулировку")
            : new ItemAssessment(ItemReadinessEnum.Ready, string.Empty);
    }

    /// <summary>
    /// Приводит уверенность к high, medium, low или пустой строке для любого другого значения
    /// </summary>
    /// <param name="confidence">Значение из файла заданий</param>
    /// <returns>Известное значение или пустая строка</returns>
    private static string Normalize(string confidence)
    {
        var value = confidence.Trim().ToLowerInvariant();

        return value is "high" or "medium" or "low" ? value : string.Empty;
    }

    /// <summary>
    /// Оценка «Нужно подтвердить» с вопросом
    /// </summary>
    /// <param name="question">Вопрос владельцу</param>
    /// <returns>Оценка</returns>
    private static ItemAssessment Ask(string question) => new(ItemReadinessEnum.NeedsConfirmation, question);
}
