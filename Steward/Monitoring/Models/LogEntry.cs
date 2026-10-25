namespace Steward.Monitoring.Models;

/// <summary>
/// Одна запись журнала, уже переведённая на человеческий язык
/// </summary>
/// <param name="Time">Местное время прогона</param>
/// <param name="Tone">Тон записи: тихий прогон, изменение или сбой</param>
/// <param name="Title">Короткая строка о том, чем закончился прогон</param>
/// <param name="Detail">Подробности, строки разделены переводом строки</param>
/// <param name="Repeat">Сколько одинаковых тихих прогонов схлопнуто в эту запись</param>
public sealed record LogEntry(DateTimeOffset Time, LogToneEnum Tone, string Title, string Detail, int Repeat);
