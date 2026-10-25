using System.Text.Json.Serialization;

namespace Steward.Status.Running;

/// <summary>
/// Запись журнала об одном запуске
/// </summary>
/// <param name="JobId">Идентификатор задачи</param>
/// <param name="Title">Название задачи</param>
/// <param name="Reason">Почему запущена: start, daily, interval или manual</param>
/// <param name="StartedAt">Когда запуск начался</param>
/// <param name="FinishedAt">Когда запуск закончился</param>
/// <param name="ExitCode">
/// Код завершения программы или <see langword="null"/>, если она не завершилась сама
/// </param>
/// <param name="Outcome">Итог запуска</param>
/// <param name="Detail">
/// Пояснение к ошибке или <see langword="null"/>
/// </param>
/// <param name="Summary">Сводка, которую записал процесс или раннер: что сделано за запуск</param>
/// <param name="Output">Хвост вывода процесса или <see langword="null"/>, если вывода не было</param>
public sealed record RunRecord(
    string JobId,
    string Title,
    string Reason,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    int? ExitCode,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] RunOutcomeEnum Outcome,
    string? Detail,
    string? Summary = null,
    string? Output = null);
