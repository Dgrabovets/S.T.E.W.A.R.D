using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Steward.Status.Common;
using Steward.Status.Running;

namespace Steward.Hosting;

/// <summary>
/// Открывает приложение Claude с готовой просьбой починить неудавшийся запуск. Просьба появляется в поле ввода, отправляет её человек
/// </summary>
public static class ClaudeFixLauncher
{
    /// <summary>
    /// Открывает новую сессию Claude Code в папке репозитория
    /// </summary>
    /// <param name="record">Неудавшийся запуск из журнала</param>
    /// <returns>Пустая строка, если приложение открыто, иначе причина, по которой не получилось</returns>
    public static string Open(RunRecord record)
    {
        try
        {
            var repoRoot = DataLocation.RepoRootOrStart();
            var directory = Path.Combine(DataLocation.DataDirectory(), "tmp", "fix");
            Directory.CreateDirectory(directory);

            var reportPath = Path.Combine(directory, $"{record.JobId}-{record.StartedAt.ToLocalTime():yyyyMMdd-HHmmss}.md");
            File.WriteAllText(reportPath, FixPrompt.Report(record));

            Process.Start(new ProcessStartInfo(BuildUrl(FixPrompt.Build(record, repoRoot, reportPath), repoRoot)) { UseShellExecute = true });

            return string.Empty;
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or UnauthorizedAccessException)
        {
            return $"Не удалось открыть Claude: {exception.Message}";
        }
    }

    /// <summary>
    /// Собирает ссылку <c>claude://code/new</c> с папкой и текстом просьбы
    /// </summary>
    /// <param name="prompt">Текст просьбы</param>
    /// <param name="repoRoot">Абсолютный путь к папке репозитория</param>
    /// <returns>Ссылка, которую открывает приложение Claude</returns>
    public static string BuildUrl(string prompt, string repoRoot) =>
        $"claude://code/new?folder={Uri.EscapeDataString(repoRoot)}&q={Uri.EscapeDataString(prompt)}";
}