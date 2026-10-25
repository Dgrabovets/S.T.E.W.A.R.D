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
    public HomeworkFormatsViewModel(IReadOnlyList<(Lesson Lesson, HomeworkFormatEnum Format)> lessons, Action<string, HomeworkFormatEnum> chosen)
    {
        Lessons = [.. lessons.Select(item => new LessonFormatViewModel(item.Lesson, item.Format, chosen))];
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