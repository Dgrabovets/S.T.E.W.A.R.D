namespace Steward.Lms.Models;

/// <summary>
/// Задание или тест из LMS с актуальным состоянием: срок, сдано ли
/// </summary>
/// <param name="Id">Номер работы на сайте LMS (номер модуля курса)</param>
/// <param name="Kind">Задание или тест</param>
/// <param name="CourseId">Номер курса</param>
/// <param name="Subject">Название предмета без преподавателей и группы</param>
/// <param name="Section">Тема или раздел курса, в котором лежит работа. Пусто, если не указан</param>
/// <param name="Name">Название работы</param>
/// <param name="Due">Последний срок сдачи или закрытия теста. <see langword="null"/>, если срока нет</param>
/// <param name="Status">Состояние как его пишет LMS, например «Отправлено для оценивания» или «Нет ответа на задание»</param>
/// <param name="Grade">Оценка как её пишет LMS. Пусто, если оценки нет</param>
/// <param name="Done">Работа сдана: задание отправлено или тест пройден и оценён</param>
/// <param name="Url">Адрес страницы работы в LMS</param>
public sealed record LmsActivity(string Id, ActivityKindEnum Kind, int CourseId, string Subject, string Section, string Name, DateTime? Due, string Status, string Grade, bool Done, string Url)
{
    /// <summary>
    /// Внешний ключ задачи в SingularityApp: по нему программа находит задачу, не создавая дубль
    /// </summary>
    public string ExternalId => $"lms:{(Kind == ActivityKindEnum.Quiz ? "quiz" : "assign")}:{Id}";
}
