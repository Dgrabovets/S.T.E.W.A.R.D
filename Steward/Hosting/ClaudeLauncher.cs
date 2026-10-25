using System.ComponentModel;
using System.Diagnostics;
using Steward.Status.Common;

namespace Steward.Hosting;

/// <summary>
/// Открывает приложение Claude с готовой просьбой. Просьба появляется в поле ввода, отправляет её человек
/// </summary>
public static class ClaudeLauncher
{
    /// <summary>
    /// Открывает новую сессию Claude Code в папке репозитория
    /// </summary>
    /// <param name="prompt">Текст просьбы</param>
    /// <returns>Пустая строка, если приложение открыто, иначе причина, по которой не получилось</returns>
    public static string Open(string prompt)
    {
        try
        {
            Process.Start(new ProcessStartInfo(BuildUrl(prompt, DataLocation.RepoRootOrStart())) { UseShellExecute = true });

            return string.Empty;
        }
        catch (Win32Exception exception)
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