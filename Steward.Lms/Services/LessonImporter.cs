using System.Text.RegularExpressions;
using Steward.Homework.Models;
using Steward.Lms.Models;

namespace Steward.Lms.Services;

/// <summary>
/// Превращает вебинары LMS в занятия для домашних заданий
/// </summary>
public static partial class LessonImporter
{
    /// <summary>
    /// Собирает занятие из вебинара
    /// </summary>
    /// <param name="webinar">Вебинар со страницы LMS</param>
    /// <returns>Занятие: ключ из номера записи, название предмета и подпись без даты</returns>
    public static Lesson ToLesson(Webinar webinar)
    {
        var course = CourseName().Match(webinar.Title);
        var title = course.Success ? course.Groups[1].Value.Trim() : webinar.Title;
        var label = LabelOf(webinar.Title);

        return new Lesson("e" + webinar.SessionId, title, DateOnly.FromDateTime(webinar.Start), label, webinar.RecordUrl, webinar.Minutes);
    }

    /// <summary>
    /// Достаёт из названия вебинара подпись занятия и убирает из неё дату и служебный номер
    /// </summary>
    /// <param name="title">Полное название вебинара, например «…]_Лекция 1, 07.09.2026» или «…]_14.09.2026_1»</param>
    /// <returns>«Лекция 1» или пустая строка, если после даты ничего не осталось</returns>
    public static string LabelOf(string title)
    {
        var index = title.LastIndexOf("]_", StringComparison.Ordinal);
        var tail = index >= 0 ? title[(index + 2)..] : string.Empty;

        return TrailingDate().Replace(tail, string.Empty).Trim(' ', ',', '_');
    }

    /// <summary>
    /// Выбирает, какую отметку получает новое занятие: пробные записи короче порога сразу не забираются
    /// </summary>
    /// <param name="lesson">Новое занятие</param>
    /// <param name="minMinutes">Порог длительности в минутах</param>
    /// <returns>«Не забирать» для короткой записи, иначе «Ждёт решения»</returns>
    public static LessonStateEnum InitialState(Lesson lesson, int minMinutes) =>
        lesson.Minutes > 0 && lesson.Minutes < minMinutes ? LessonStateEnum.Skip : LessonStateEnum.Undecided;

    /// <summary>
    /// Название предмета между «LMS_P_» и первой квадратной скобкой
    /// </summary>
    /// <returns>Регулярное выражение с одной группой</returns>
    [GeneratedRegex("^LMS_P_(.*?)\\s*\\[", RegexOptions.CultureInvariant)]
    private static partial Regex CourseName();

    /// <summary>
    /// Дата в конце подписи и номер после неё
    /// </summary>
    /// <returns>Регулярное выражение даты вида 07.09.2026 с необязательным «_1»</returns>
    [GeneratedRegex("[\\s,_]*\\d{2}\\.\\d{2}\\.\\d{4}(_\\d+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingDate();
}
