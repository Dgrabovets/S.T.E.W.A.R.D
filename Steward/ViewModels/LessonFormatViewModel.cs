using System.Globalization;
using Steward.Homework.Models;

namespace Steward.ViewModels;

/// <summary>
/// Строка окна форматов: занятие и выбор, в каком виде готовить по нему ДЗ
/// </summary>
public sealed class LessonFormatViewModel
{
    /// <summary>
    /// Создаёт строку
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="format">Выбранный сейчас формат</param>
    /// <param name="chosen">Вызывается, когда выбран другой формат: параметры — ключ занятия и формат</param>
    public LessonFormatViewModel(Lesson lesson, HomeworkFormatEnum format, Action<string, HomeworkFormatEnum> chosen)
    {
        Title = lesson.Title;
        Date = lesson.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        var options = new List<FormatOptionViewModel>();
        foreach (var option in HomeworkFormats.All)
        {
            options.Add(new FormatOptionViewModel(option, option == format, picked =>
            {
                foreach (var other in options)
                    other.IsSelected = other.Format == picked;

                chosen(lesson.Id, picked);
            }));
        }

        Options = options;
    }

    /// <summary>
    /// Название предмета и темы
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// День занятия
    /// </summary>
    public string Date { get; }

    /// <summary>
    /// Форматы на выбор
    /// </summary>
    public IReadOnlyList<FormatOptionViewModel> Options { get; }
}