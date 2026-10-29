namespace Steward.Homework.Models;

/// <summary>
/// Одно задание, которое агент нашёл в расшифровке занятия
/// </summary>
/// <param name="Title">Что нужно сделать, одной фразой</param>
/// <param name="Due">Срок сдачи или <see langword="null"/>, если на занятии его не назвали</param>
/// <param name="Requirements">Требования к работе: объём, оформление, что обязательно включить</param>
/// <param name="Link">Ссылка на задание, если её назвали, иначе пусто</param>
/// <param name="Done">Задание уже сдано: по данным LMS работа по этому заданию отправлена или студент отметил его сданным сам. Такие задания не готовятся</param>
/// <param name="ActivityId">Номер работы в LMS, к которой относится задание, или пусто, если агент её не определил. По нему открывается страница задания и берётся условие</param>
/// <param name="Submitted">Задание отметил сам студент. Эту отметку можно снять, в отличие от сдачи по данным LMS</param>
/// <param name="Changed">Дата, когда преподаватель изменил условие работы в LMS, или <see langword="null"/>, если правок не было</param>
/// <param name="Evidence">Дословная цитата из расшифровки до 300 знаков, на которой основано задание, или пусто</param>
/// <param name="Confidence">Уверенность агента: «high», «medium», «low» или пусто, если не названа</param>
/// <param name="Doubt">Одной строкой, в чём сомнение агента, или пусто</param>
public sealed record HomeworkItem(string Title, DateOnly? Due, string Requirements, string Link, bool Done = false, string ActivityId = "", bool Submitted = false, DateOnly? Changed = null, string Evidence = "", string Confidence = "", string Doubt = "");