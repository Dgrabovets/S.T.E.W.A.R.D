namespace Steward.Lms.Services;

/// <summary>
/// Ожидаемый сбой работы с LMS или MTS Link: не пустили, страница не такая, как раньше. Текст безопасно показать в журнале
/// </summary>
/// <param name="message">Что случилось и что сделать, без паролей и адресов с токенами</param>
public sealed class LmsException(string message) : Exception(message);
