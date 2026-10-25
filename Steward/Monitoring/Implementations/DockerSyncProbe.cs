using Steward.Monitoring.Models;
using System.Diagnostics;
using System.IO;

namespace Steward.Monitoring.Implementations;

/// <summary>
/// Спрашивает Docker, запущен ли контейнер синхронизации, и забирает хвост журнала
/// </summary>
public sealed class DockerSyncProbe
{
    /// <summary>
    /// Каталог с <c>docker-compose.yml</c>
    /// </summary>
    private readonly string _repoRoot;

    #region Location

    /// <summary>
    /// Создаёт опрос для каталога репозитория
    /// </summary>
    /// <param name="repoRoot">
    /// Каталог, в котором лежит <c>docker-compose.yml</c>
    /// </param>
    public DockerSyncProbe(string repoRoot)
    {
        _repoRoot = repoRoot;
    }

    #endregion Location

    #region Commands

    /// <summary>
    /// Читает состояние сервиса <c>schedule</c>
    /// </summary>
    /// <param name="cancellationToken">Отменяет ожидание ответа Docker</param>
    /// <returns>Снимок для иконки</returns>
    public async Task<SyncSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        var state = await RunAsync("docker", "compose ps -a --format {{.Service}}|{{.State}}", cancellationToken);
        var logs = state.ExitCode == 0
                     ? await RunAsync("docker", "compose logs --tail 250 --timestamps schedule", cancellationToken)
                     : new CommandResult(state.ExitCode, string.Empty);

        var serviceState = string.Empty;
        if (state.ExitCode == 0)
        {
            foreach (var line in state.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = line.Split('|', 2);
                if (parts.Length == 2 && string.Equals(parts[0], "schedule", StringComparison.OrdinalIgnoreCase))
                    serviceState = parts[1];
            }
        }

        return SyncLogInterpreter.Interpret(state.ExitCode == 0, serviceState, logs.Text);
    }

    /// <summary>
    /// Пересоздаёт контейнер, чтобы он прочитал файл настроек заново
    /// </summary>
    /// <param name="cancellationToken">Отменяет ожидание Docker</param>
    /// <returns>
    /// <see langword="true"/>, если контейнер перезапущен
    /// </returns>
    public async Task<bool> RestartAsync(CancellationToken cancellationToken)
    {
        var result = await RunAsync("docker", "compose up -d --force-recreate --no-build", cancellationToken);
        return result.ExitCode == 0;
    }

    /// <summary>
    /// Запускает команду Docker и собирает её вывод
    /// </summary>
    /// <param name="fileName">Имя программы</param>
    /// <param name="arguments">Аргументы без кавычек оболочки</param>
    /// <param name="cancellationToken">Отменяет ожидание</param>
    /// <returns>Код выхода и объединённый вывод</returns>
    private async Task<CommandResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        Process? process = null;
        try
        {
            process = new Process
            {
                StartInfo = new ProcessStartInfo(fileName, arguments)
                {
                    WorkingDirectory = _repoRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8,
                },
            };

            if (!process.Start())
                return new CommandResult(1, "Не удалось запустить docker");

            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            return new CommandResult(process.ExitCode, (await output + await error).Trim());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new CommandResult(1, exception.Message);
        }
        finally
        {
            process?.Dispose();
        }
    }

    #endregion Commands

    #region Result

    /// <summary>
    /// Код и текст одной команды
    /// </summary>
    /// <param name="ExitCode">Код выхода процесса</param>
    /// <param name="Text">Стандартный вывод и стандартный поток ошибок</param>
    private sealed record CommandResult(int ExitCode, string Text);

    #endregion Result
}
