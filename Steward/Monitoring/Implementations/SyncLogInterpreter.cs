using Steward.Monitoring.Models;

namespace Steward.Monitoring.Implementations;

/// <summary>
/// Собирает снимок иконки из ответа Docker и хвоста журнала
/// </summary>
public static class SyncLogInterpreter
{
    #region Snapshot

    /// <summary>
    /// Собирает снимок из ответа Docker и хвоста журнала
    /// </summary>
    /// <param name="dockerAvailable">
    /// <see langword="true"/>, если команда состояния контейнера выполнилась
    /// </param>
    /// <param name="serviceState">
    /// Состояние сервиса <c>schedule</c>, например <c>running</c>. Пустая строка, если контейнера нет
    /// </param>
    /// <param name="logs">
    /// Журнал с метками времени
    /// </param>
    /// <returns>
    /// Состояние для иконки и окна
    /// </returns>
    public static SyncSnapshot Interpret(bool dockerAvailable, string serviceState, string logs)
    {
        var entries = LogJournal.Read(logs);
        var running = dockerAvailable && string.Equals(serviceState, "running", StringComparison.OrdinalIgnoreCase);
        var latestProblem = entries.Count > 0 && entries[0].Tone == LogTone.Problem ? entries[0] : null;

        return new SyncSnapshot
        {
            DockerAvailable = dockerAvailable,
            ContainerRunning = running,
            LastSuccess = entries.FirstOrDefault(entry => entry.Tone != LogTone.Problem)?.Time,
            CurrentError = latestProblem is null
                ? null
                : latestProblem.Detail.Length == 0 ? latestProblem.Title : latestProblem.Title + "\n" + latestProblem.Detail,
            Entries = entries,
        };
    }

    #endregion Snapshot
}
