using System.Diagnostics;
using Steward.Reels.Models;

namespace Steward.Reels.Services;

/// <summary>
/// Запускает внешнюю программу без окна и собирает её вывод
/// </summary>
public static class CommandRunner
{
    /// <summary>
    /// Запускает программу и ждёт её не дольше таймаута
    /// </summary>
    /// <param name="fileName">Программа</param>
    /// <param name="arguments">Аргументы по одному: кавычки подбирает система</param>
    /// <param name="timeout">Сколько ждать</param>
    /// <param name="cancellationToken">Останавливает программу</param>
    /// <returns>Код завершения и оба потока вывода</returns>
    /// <exception cref="ReelException">Программа не запустилась или не уложилась во время</exception>
    public static async Task<CommandResult> RunAsync(string fileName, IEnumerable<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new ReelException($"Не удалось запустить {Path.GetFileName(fileName)}: {exception.Message}");
        }

        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);

            throw new ReelException($"{Path.GetFileName(fileName)} не уложился в {timeout.TotalMinutes:0.##} мин");
        }

        return new CommandResult(process.ExitCode, await output, await error);
    }
}