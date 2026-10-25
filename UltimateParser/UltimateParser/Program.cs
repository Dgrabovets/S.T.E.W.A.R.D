using Microsoft.Extensions.Hosting;
using UltimateParser.Sync.Implementations;

namespace UltimateParser;

/// <summary>
/// Запускает сверку расписания в контейнере
/// </summary>
public static class Program
{
    /// <summary>
    /// Точка входа службы
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
        var builder = Host.CreateApplicationBuilder(args.Where(arg => arg != "--once").ToArray());
        builder.Services.AddScheduleSync(runOnce);
        await builder.Build().RunAsync();
    }
}
