namespace Steward.Status.Running;

/// <summary>
/// Итог одного запущенного процесса, пока он ещё не записан в статус и журнал
/// </summary>
/// <param name="Outcome">Чем закончился запуск</param>
/// <param name="ExitCode">Код завершения или <see langword="null"/>, если процесс не завершился сам</param>
/// <param name="Detail">Пояснение к ошибке или <see langword="null"/></param>
/// <param name="Output">Хвост вывода процесса: стандартный вывод и поток ошибок. Пусто, если вывода не было</param>
internal sealed record ProcessResult(RunOutcomeEnum Outcome, int? ExitCode, string? Detail, string? Output = null);