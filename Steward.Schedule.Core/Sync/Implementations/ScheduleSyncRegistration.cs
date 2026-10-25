using Microsoft.Extensions.DependencyInjection;
using Steward.Schedule.Common.Helpers;
using Steward.Schedule.Scheduling.Implementations;
using Steward.Schedule.Scheduling.Interfaces;
using Steward.Schedule.Singularity.Implementations;
using Steward.Schedule.Singularity.Interfaces;
using Steward.Schedule.Sync.Interfaces;

namespace Steward.Schedule.Sync.Implementations;

/// <summary>
/// Подключает сверку расписания к хосту приложения
/// </summary>
public static class ScheduleSyncRegistration
{
    /// <summary>
    /// Пауза HTTP-запроса к таблице и к Singularity
    /// </summary>
    private static readonly TimeSpan REQUEST_TIMEOUT = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Адрес REST API Singularity
    /// </summary>
    private const string SINGULARITY_API = "https://api.singularity-app.com";

    #region Registration

    /// <summary>
    /// Читает настройки и регистрирует чтение таблицы, клиент Singularity и цикл сверки
    /// </summary>
    /// <param name="services">Коллекция служб хоста</param>
    /// <param name="runOnce">
    /// <see langword="true"/>, если нужен один прогон
    /// </param>
    /// <returns>Та же коллекция</returns>
    public static IServiceCollection AddScheduleSync(this IServiceCollection services, bool runOnce)
    {
        services.AddSingleton(EnviromentalHelper.GetSyndOptionsFromEnvironment(runOnce));
        services.AddHttpClient<IScheduleSource, CsvScheduleReader>(client =>
        {
            client.Timeout = REQUEST_TIMEOUT;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Steward.Schedule/1.0");
        });
        services.AddHttpClient<ISingularityClient, SingularityClient>(client =>
        {
            client.BaseAddress = new Uri(SINGULARITY_API);
            client.Timeout = REQUEST_TIMEOUT;
        });
        services.AddSingleton<IScheduleClock, TimeZoneClock>();
        services.AddSingleton<ScheduleSyncer>();
        services.AddHostedService<SyncHostedService>();
        return services;
    }

    #endregion Registration
}
