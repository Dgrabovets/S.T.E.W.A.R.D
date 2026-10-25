namespace Steward.Status;

/// <summary>
/// Статус одного процесса. Лежит в файле <c>status/&lt;id&gt;.json</c>, его пишет сам процесс или раннер, читает приложение в трее
/// </summary>
public sealed record ProcessStatus
{
    /// <summary>
    /// Версия формата. Читатель, встретивший незнакомые поля, их игнорирует
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Короткий идентификатор из латиницы, цифр, дефиса и подчёркивания. Он же имя файла
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Название для людей, например «Почта»
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Текущее состояние
    /// </summary>
    public ProcessState State { get; init; }

    /// <summary>
    /// Когда файл записан в последний раз. По нему раннер понимает, что процесс сам обновил статус
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Когда был последний запуск
    /// </summary>
    public DateTimeOffset? LastRunAt { get; init; }

    /// <summary>
    /// Когда последний запуск закончился успешно
    /// </summary>
    public DateTimeOffset? LastSuccessAt { get; init; }

    /// <summary>
    /// Когда процесс запустится в следующий раз, если он это знает
    /// </summary>
    public DateTimeOffset? NextRunAt { get; init; }

    /// <summary>
    /// Итог последнего запуска одной строкой
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Текст ошибки, если запуск не удался
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Сколько результатов ждёт решения пользователя: идеи, черновики ДЗ
    /// </summary>
    public int Awaiting { get; init; }
}
