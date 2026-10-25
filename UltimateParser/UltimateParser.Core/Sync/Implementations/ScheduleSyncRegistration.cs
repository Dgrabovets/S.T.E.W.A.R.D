using Microsoft.Extensions.DependencyInjection;
using UltimateParser.Common.Helpers;
using UltimateParser.Scheduling.Implementations;
using UltimateParser.Scheduling.Interfaces;
using UltimateParser.Singularity.Implementations;
using UltimateParser.Singularity.Interfaces;
using UltimateParser.Sync.Interfaces;

namespace UltimateParser.Sync.Implementations;

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
    /// <param name="services">
    /// Коллекция служб хоста
    /// </param>
    /// <param name="runOnce">
    /// <see langword="true"/>, если нужен один прогон
    /// </param>
    /// <returns>
    /// Та же коллекция
    /// </returns>
    public static IServiceCollection AddScheduleSync(this IServiceCollection services, bool runOnce)
    {
        services.AddSingleton(EnviromentalHelper.GetSyndOptionsFromEnvironment(runOnce));
        services.AddHttpClient<IScheduleSource, CsvScheduleReader>(client =>
        {
            client.Timeout = REQUEST_TIMEOUT;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UltimateParser/1.0");
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
