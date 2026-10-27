namespace Steward.Lms.Models;

/// <summary>
/// Расшифровка записи занятия из MTS Link: краткое резюме и подробный текст по репликам
/// </summary>
/// <param name="Summary">Резюме встречи: тема, краткое содержание, ключевые моменты. Пусто, если сервис его не сделал</param>
/// <param name="Lines">Реплики в порядке записи</param>
public sealed record MtsTranscript(string Summary, IReadOnlyList<TranscriptLine> Lines);

/// <summary>
/// Одна реплика расшифровки
/// </summary>
/// <param name="Offset">Время от начала записи</param>
/// <param name="Speaker">Имя говорящего</param>
/// <param name="Text">Сказанное</param>
public sealed record TranscriptLine(TimeSpan Offset, string Speaker, string Text);
