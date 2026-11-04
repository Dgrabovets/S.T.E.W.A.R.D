namespace Steward.Lms.Models;

/// <summary>
/// Прошедший вебинар со страницы «Ссылка на занятия МТС Линк» в LMS
/// </summary>
/// <param name="Title">Полное название вебинара в MTS Link</param>
/// <param name="Start">Когда вебинар начался, по местному времени без часового пояса</param>
/// <param name="Minutes">Длительность записи в минутах</param>
/// <param name="RecordUrl">Адрес записи на my.mts-link.ru</param>
/// <param name="EventId">Номер мероприятия из адреса записи</param>
/// <param name="SessionId">Номер записи из адреса записи: он же ключ занятия</param>
public sealed record Webinar(string Title, DateTime Start, int Minutes, string RecordUrl, string EventId, string SessionId);
