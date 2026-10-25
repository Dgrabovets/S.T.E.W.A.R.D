namespace Steward.Homework.Models;

/// <summary>
/// Одно задание, которое агент нашёл в расшифровке занятия
/// </summary>
/// <param name="Title">Что нужно сделать, одной фразой</param>
/// <param name="Due">Срок сдачи или <see langword="null"/>, если на занятии его не назвали</param>
/// <param name="Requirements">Требования к работе: объём, оформление, что обязательно включить</param>
/// <param name="Link">Ссылка на задание, если её назвали, иначе пусто</param>
public sealed record HomeworkItem(string Title, DateOnly? Due, string Requirements, string Link);