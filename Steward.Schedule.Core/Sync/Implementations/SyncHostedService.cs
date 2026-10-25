using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Steward.Schedule.Common.Models;

namespace Steward.Schedule.Sync.Implementations;

/// <summary>
/// Запускает <see cref="ScheduleSyncer"/> один раз или в цикле, пока процесс не остановят
/// </summary>
public sealed class SyncHostedService : BackgroundService
{
    /// <summary>
    /// Операция, которая читает таблицу и пишет задачи в Singularity
    /// </summary>
    private readonly ScheduleSyncer _sync;

    /// <summary>
    /// Выбирает одиночный прогон и паузу между повторами
    /// </summary>
    private readonly SyncOptions _options;

    /// <summary>
    /// Останавливает хост после одиночного прогона
    /// </summary>
    private readonly IHostApplicationLifetime _lifetime;

    /// <summary>
    /// Принимает ошибки, которые иначе оборвали бы цикл
    /// </summary>
    private readonly ILogger<SyncHostedService> _logger;

    /// <summary>
    /// Создаёт цикл синхронизации внутри хоста
    /// </summary>
    /// <param name="sync">Операция, которая читает таблицу и пишет задачи в Singularity</param>
    /// <param name="options">Выбирает одиночный прогон и паузу между повторами</param>
    /// <param name="lifetime">Останавливает хост после одиночного прогона</param>
    /// <param name="logger">Принимает ошибки, которые иначе оборвали бы цикл</param>
    public SyncHostedService(
        ScheduleSyncer sync,
        SyncOptions options,
        IHostApplicationLifetime lifetime,
        ILogger<SyncHostedService> logger)
    {
        _sync = sync;
        _options = options;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <summary>
    /// Сразу выполняет синхронизацию, затем завершается или ждёт <see cref="SyncOptions.IntervalMinutes"/>
    /// </summary>
    /// <param name="stoppingToken">Отменяется, когда хост останавливается</param>
    /// <returns>Задача, которая завершается вместе с циклом</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        #region One shot

        if (_options.RunOnce)
        {
            try
            {
                await _sync.RunAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Sync failed");
                Environment.ExitCode = 1;
            }

            _lifetime.StopApplication();
            return;
        }

        #endregion One shot

        #region Interval loop

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _sync.RunAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Sync failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_options.IntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        #endregion Interval loop
    }
}
