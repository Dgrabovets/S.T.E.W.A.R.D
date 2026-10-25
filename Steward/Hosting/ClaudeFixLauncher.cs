using System.IO;
using Steward.Status.Common;
using Steward.Status.Running;

namespace Steward.Hosting;

/// <summary>
/// Открывает Claude с просьбой починить неудавшийся запуск
/// </summary>
public static class ClaudeFixLauncher
{
    /// <summary>
    /// Записывает отчёт о запуске и открывает новую сессию Claude Code с просьбой
    /// </summary>
    /// <param name="record">Неудавшийся запуск из журнала</param>
    /// <returns>Пустая строка, если приложение открыто, иначе причина, по которой не получилось</returns>
    public static string Open(RunRecord record)
    {
        try
        {
            var directory = Path.Combine(DataLocation.DataDirectory(), "tmp", "fix");
            Directory.CreateDirectory(directory);

            var reportPath = Path.Combine(directory, $"{record.JobId}-{record.StartedAt.ToLocalTime():yyyyMMdd-HHmmss}.md");
            File.WriteAllText(reportPath, FixPrompt.Report(record));

            return ClaudeLauncher.Open(FixPrompt.Build(record, DataLocation.RepoRootOrStart(), reportPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"Не удалось подготовить отчёт: {exception.Message}";
        }
    }
}