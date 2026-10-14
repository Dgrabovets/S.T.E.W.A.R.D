using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UltimateParser.Common.Models;
using UltimateParser.Scheduling.Implementations;
using UltimateParser.Scheduling.Interfaces;
using UltimateParser.Singularity.Implementations;
using UltimateParser.Singularity.Interfaces;
using UltimateParser.Sync.Implementations;
using UltimateParser.Sync.Interfaces;

namespace UltimateParser;

/// <summary>
/// Собирает и запускает хост синхронизации расписания
/// </summary>
public static class Program
{
    /// <summary>
    /// Точка входа приложения
    /// </summary>
    /// <param name="args">
    /// Аргумент <c>--once</c> выполняет один прогон и завершает процесс, иначе повтор идёт по заданному интервалу
    /// </param>
    /// <returns>
    /// Задача, которая завершается вместе с остановкой хоста
    /// </returns>
    public static async Task Main(string[] args)
    {
        var runOnce = args.Any(arg => arg == "--once");
        var options = SyncOptions.FromEnvironment(runOnce);

        var builder = Host.CreateApplicationBuilder(args.Where(arg => arg != "--once").ToArray());
        builder.Services.AddSingleton(options);
        builder.Services.AddHttpClient<IScheduleSource, CsvScheduleReader>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UltimateParser/1.0");
        });
        builder.Services.AddHttpClient<ISingularityClient, SingularityClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.singularity-app.com");
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        builder.Services.AddSingleton<IScheduleClock, TimeZoneClock>();
        builder.Services.AddSingleton<ScheduleSync>();
        builder.Services.AddHostedService<SyncHostedService>();

        await builder.Build().RunAsync();
    }
}
