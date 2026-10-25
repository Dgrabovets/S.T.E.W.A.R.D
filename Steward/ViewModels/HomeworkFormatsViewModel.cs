using Steward.Homework.Models;

namespace Steward.ViewModels;

/// <summary>
/// Окно выбора формата ДЗ для каждого занятия
/// </summary>
public sealed class HomeworkFormatsViewModel
{
    /// <summary>
    /// Создаёт модель окна
    /// </summary>
    /// <param name="lessons">Занятия с выбранными форматами</param>
    /// <param name="chosen">Вызывается при выборе формата: параметры — ключ занятия и формат</param>
    /// <param name="hasTranscript">Проверяет, есть ли у занятия расшифровка</param>
    /// <param name="prepare">Вызывается по кнопке «Подготовить»: параметр — занятие</param>
    public HomeworkFormatsViewModel(
        IReadOnlyList<(Lesson Lesson, HomeworkFormatEnum Format)> lessons,
        Action<string, HomeworkFormatEnum> chosen,
        Func<Lesson, bool> hasTranscript,
        Action<Lesson> prepare)
    {
        Lessons = [.. lessons.Select(item => new LessonFormatViewModel(item.Lesson, item.Format, chosen, hasTranscript(item.Lesson), prepare))];
    }

    /// <summary>
    /// Строки занятий
    /// </summary>
    public IReadOnlyList<LessonFormatViewModel> Lessons { get; }

    /// <summary>
    /// Занятий пока нет
    /// </summary>
    public bool IsEmpty => Lessons.Count == 0;
}