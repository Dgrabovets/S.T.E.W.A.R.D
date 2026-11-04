namespace Steward.Models;

/// <summary>
/// Часовой пояс для выпадающего списка
/// </summary>
/// <param name="Id">Идентификатор пояса в формате IANA, например Europe/Moscow: его читает сервис расписания</param>
/// <param name="Title">Название для человека: смещение от UTC и города</param>
public sealed record TimeZoneChoice(string Id, string Title);
