namespace Steward.Reels.Models;

/// <summary>
/// Итог внешней программы
/// </summary>
/// <param name="ExitCode">Код завершения</param>
/// <param name="Output">Стандартный вывод</param>
/// <param name="Error">Поток ошибок</param>
public sealed record CommandResult(int ExitCode, string Output, string Error);