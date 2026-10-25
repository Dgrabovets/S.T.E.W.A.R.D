using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Steward.Hosting;

/// <summary>
/// Запускает Claude Code в фоне одним заходом, без чата: сессия не сохраняется, в списке чатов ничего не появляется. Подходит для чётких задач, где диалог не нужен
/// </summary>
public static class ClaudeAgent
{
    /// <summary>
    /// Модель по умолчанию: для поиска заданий в тексте хватает лёгкой
    /// </summary>
    public const string MODEL = "sonnet";

    /// <summary>
    /// Сколько минут ждать ответа, потом запуск прерывается
    /// </summary>
    private const int TIMEOUT_MINUTES = 10;

    /// <summary>
    /// Потолок расхода на один запуск, в долларах API. Для подписки это только защитный предел
    /// </summary>
    private const string MAX_BUDGET_USD = "1";

    /// <summary>
    /// Сколько символов вывода ошибок оставлять в сообщении
    /// </summary>
    private const int ERROR_LIMIT = 400;

    /// <summary>
    /// Собирает аргументы запуска: только чтение и запись файлов, правки без вопросов внутри рабочей папки, без сохранения сессии
    /// </summary>
    /// <param name="prompt">Просьба</param>
    /// <param name="workDirectory">Папка, где агенту разрешено читать и писать</param>
    /// <returns>Аргументы командной строки по одному</returns>
    public static IReadOnlyList<string> Arguments(string prompt, string workDirectory) =>
    [
        "-p", prompt,
        "--model", MODEL,
        "--tools", "Read,Write",
        "--permission-mode", "acceptEdits",
        "--add-dir", workDirectory,
        "--no-session-persistence",
        "--max-budget-usd", MAX_BUDGET_USD,
    ];

    /// <summary>
    /// Ищет программу Claude Code: сначала в PATH, потом среди версий, которые ставит приложение Claude
    /// </summary>
    /// <returns>Путь к <c>claude.exe</c> или <see langword="null"/>, если не найдено</returns>
    public static string? FindExecutable()
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim(), "claude.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        var bundled = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Claude-Data");

        return Directory.Exists(bundled)
            ? Directory.EnumerateFiles(bundled, "claude.exe", SearchOption.AllDirectories)
                .Where(path => path.Contains($"{Path.DirectorySeparatorChar}claude-code{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
                .FirstOrDefault()
            : null;
    }

    /// <summary>
    /// Запускает Claude с просьбой и ждёт конца
    /// </summary>
    /// <param name="prompt">Просьба</param>
    /// <param name="workDirectory">Рабочая папка агента</param>
    /// <param name="cancellationToken">Отменяет запуск</param>
    /// <returns>Пустая строка, если Claude отработал без ошибки, иначе причина</returns>
    public static async Task<string> RunAsync(string prompt, string workDirectory, CancellationToken cancellationToken)
    {
        var executable = FindExecutable();
        if (executable is null)
            return "Не найден claude.exe: установи приложение Claude или добавь его в PATH";

        Directory.CreateDirectory(workDirectory);

        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in Arguments(prompt, workDirectory))
            start.ArgumentList.Add(argument);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(TIMEOUT_MINUTES));

        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Процесс не запустился");
            var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var errors = process.StandardError.ReadToEndAsync(CancellationToken.None);

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);

                return cancellationToken.IsCancellationRequested ? "Запуск отменён" : $"Claude не ответил за {TIMEOUT_MINUTES} минут";
            }

            await Task.WhenAll(output, errors);

            return process.ExitCode == 0 ? string.Empty : Tail(errors.Result.Length > 0 ? errors.Result : output.Result, process.ExitCode);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException)
        {
            return $"Не удалось запустить Claude: {exception.Message}";
        }
    }

    /// <summary>
    /// Формирует короткое сообщение об ошибке из хвоста вывода
    /// </summary>
    /// <param name="text">Вывод Claude</param>
    /// <param name="exitCode">Код завершения</param>
    /// <returns>Текст ошибки не длиннее лимита</returns>
    private static string Tail(string text, int exitCode)
    {
        var trimmed = text.Trim();

        return $"Claude завершился с кодом {exitCode}: {(trimmed.Length > ERROR_LIMIT ? trimmed[^ERROR_LIMIT..] : trimmed)}";
    }
}