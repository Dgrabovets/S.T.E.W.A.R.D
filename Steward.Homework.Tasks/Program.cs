using System.Globalization;
using Microsoft.Extensions.Configuration;
using Steward.Homework.Services;
using Steward.Homework.Storage;
using Steward.Mail.Common;
using Steward.Mail.Services;
using Steward.Status.Common;
using Steward.Status.State;

namespace Steward.Homework.Tasks;

/// <summary>
/// Создаёт задачи SingularityApp по домашним заданиям, которые агент нашёл в расшифровках занятий
/// Запускается приложением в трее. Только создаёт задачи: работу не готовит и ничего не сдаёт
/// </summary>
internal static class Program
{
    /// <summary>
    /// Идентификатор процесса в общем формате статусов, он же id задачи в config/jobs.json
    /// </summary>
    private const string STATUS_ID = "homework";

    /// <summary>
    /// Находит новые задания и создаёт для них задачи
    /// </summary>
    /// <returns>0, если всё прошло, и 1, если случилась ошибка</returns>
    private static async Task<int> Main()
    {
        var statusStore = new StatusStore(DataLocation.StatusDirectory());

        try
        {
            var settings = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build()
                .GetSection("Singularity")
                .Get<SingularitySettings>() ?? throw new InvalidOperationException("Не удалось прочитать раздел Singularity в appsettings.json");

            if (string.IsNullOrWhiteSpace(settings.Token) || settings.Token.StartsWith('<'))
                throw new InvalidOperationException("Не задан токен в Singularity:Token");

            using var singularity = new SingularityClient(settings);
            var creator = new HomeworkTaskCreator(new HomeworkStore(Path.Combine(DataLocation.DataDirectory(), "homework")), singularity, settings.ProjectId, TimeProvider.System, Log);
            var (created, existing) = await creator.RunAsync(CancellationToken.None);

            var summary = $"Создано задач: {created}, уже были: {existing}";
            Log(summary);
            statusStore.Report(STATUS_ID, "Домашние задания", ProcessStateEnum.Ok, DateTimeOffset.Now, summary, null, success: true);

            return 0;
        }
        catch (Exception exception)
        {
            Log($"Ошибка: {exception}");
            statusStore.Report(STATUS_ID, "Домашние задания", ProcessStateEnum.Failed, DateTimeOffset.Now, null, Describe(exception), success: false);

            return 1;
        }
    }

    /// <summary>
    /// Описывает ошибку цепочкой причин: тип и сообщение каждого исключения от внешнего к внутреннему
    /// </summary>
    /// <param name="exception">Исключение</param>
    /// <returns>Строка вида «HttpRequestException: ответил 400 → SocketException: ...»</returns>
    private static string Describe(Exception exception)
    {
        var parts = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
            parts.Add($"{current.GetType().Name}: {current.Message}");

        return string.Join(" → ", parts);
    }

    /// <summary>
    /// Пишет строку с временем в консоль: программу запускает приложение, оно складывает вывод в журнал
    /// </summary>
    /// <param name="message">Текст строки</param>
    private static void Log(string message) => Console.WriteLine($"{DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture)}  {message}");
}