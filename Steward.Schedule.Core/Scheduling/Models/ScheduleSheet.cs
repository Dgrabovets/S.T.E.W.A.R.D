namespace Steward.Schedule.Scheduling.Models;

/// <summary>
/// Один лист таблицы расписания: вкладка месяца с её CSV
/// </summary>
/// <param name="Name">Название вкладки, например «НОЯБРЬ». Пусто, если лист выбран по номеру и названия не знает</param>
/// <param name="Month">Номер месяца вкладки от 1 до 12 или <see langword="null"/>, если по названию месяц не определился</param>
/// <param name="Csv">Текст листа в виде CSV, включая строку заголовков</param>
public sealed record ScheduleSheet(string Name, int? Month, string Csv);
