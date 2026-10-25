using Steward.Status.Registry;
using Steward.Status.State;

namespace Steward.Status.Tests;

/// <summary>
/// Общие заготовки для тестов
/// </summary>
public static class Samples
{
    /// <summary>
    /// Часовой пояс Белграда: сегодня, 25 октября 2026 года, он переходит на зимнее время
    /// </summary>
    public static TimeZoneInfo Belgrade
    {
        get
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Europe/Belgrade");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");
            }
        }
    }

    /// <summary>
    /// Момент в зимнем поясе Белграда (+01:00)
    /// </summary>
    /// <param name="year">Год</param>
    /// <param name="month">Месяц</param>
    /// <param name="day">День</param>
    /// <param name="hour">Час</param>
    /// <param name="minute">Минута</param>
    /// <returns>Момент со смещением +01:00</returns>
    public static DateTimeOffset Winter(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, TimeSpan.FromHours(1));

    /// <summary>
    /// Момент в летнем поясе Белграда (+02:00)
    /// </summary>
    /// <param name="year">Год</param>
    /// <param name="month">Месяц</param>
    /// <param name="day">День</param>
    /// <param name="hour">Час</param>
    /// <param name="minute">Минута</param>
    /// <returns>Момент со смещением +02:00</returns>
    public static DateTimeOffset Summer(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, TimeSpan.FromHours(2));

    /// <summary>
    /// Описание задачи с условиями запуска
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="triggers">Условия запуска</param>
    /// <returns>Включённая задача с командой-пустышкой</returns>
    public static JobDefinition Job(string id, params JobTrigger[] triggers) =>
        new() { Id = id, Title = id, Command = "cmd.exe", Arguments = "/c exit 0", Triggers = triggers };

    /// <summary>
    /// Условие «каждый день в заданное время»
    /// </summary>
    /// <param name="time">
    /// Время <c>HH:mm</c>
    /// </param>
    /// <returns>Условие</returns>
    public static JobTrigger Daily(string time) => new() { Type = JobTrigger.DAILY, At = time };

    /// <summary>
    /// Условие «через заданное число минут»
    /// </summary>
    /// <param name="minutes">Число минут</param>
    /// <returns>Условие</returns>
    public static JobTrigger Every(int minutes) => new() { Type = JobTrigger.INTERVAL, EveryMinutes = minutes };

    /// <summary>
    /// Условие «при запуске приложения»
    /// </summary>
    public static JobTrigger OnStart => new() { Type = JobTrigger.START };

    /// <summary>
    /// Статус процесса
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="state">Состояние</param>
    /// <param name="updatedAt">Когда записан</param>
    /// <returns>Статус с заполненными по умолчанию полями</returns>
    public static ProcessStatus Status(string id, ProcessStateEnum state, DateTimeOffset updatedAt) =>
        new() { Id = id, Title = id, State = state, UpdatedAt = updatedAt };
}
