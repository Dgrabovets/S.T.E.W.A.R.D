using System.Globalization;
using System.Text.Json;
using Steward.Ideas.Storage;
using Steward.Reels.Common;
using Steward.Reels.Models;
using Steward.Reels.Services;
using Steward.Status.Common;
using Steward.Status.State;

namespace Steward.Reels;

/// <summary>
/// Бот для рилсов: берёт ссылки из Telegram, расшифровывает речь и складывает идеи в <c>data/ideas</c>.
/// Запускается приложением в трее раз в несколько минут и каждый раз обрабатывает то, что пришло
/// </summary>
internal static class Program
{
    /// <summary>
    /// Идентификатор процесса в общем формате статусов, он же имя файла и id задачи в config/jobs.json
    /// </summary>
    private const string STATUS_ID = "reels";

    /// <summary>
    /// Читает сообщения, делает идеи и записывает итог в общий статус
    /// </summary>
    /// <param name="args">Аргументы не используются</param>
    /// <returns>0, если проверка прошла, и 1, если случилась ошибка</returns>
    private static async Task<int> Main(string[] args)
    {
        var statusStore = new StatusStore(DataLocation.StatusDirectory());
        var ideas = new IdeaStore(Path.Combine(DataLocation.DataDirectory(), "ideas"));

        try
        {
            var settings = LoadSettings();
            var repoRoot = DataLocation.RepoRootOrStart();
            var python = Path.Combine(repoRoot, "Whisper", ".venv", "Scripts", "python.exe");
            var work = Path.Combine(DataLocation.DataDirectory(), "reels", "work");

            using var telegram = new TelegramClient(settings.BotToken);
            var pipeline = new ReelsPipeline(
                telegram,
                new YtDlpDownloader(python, work, settings),
                new WhisperTranscriber(python, Path.Combine(repoRoot, "Whisper", "transcribe.py"), settings),
                ideas,
                new OffsetStore(Path.Combine(DataLocation.DataDirectory(), "reels", "offset.txt")),
                settings,
                TimeProvider.System,
                Log);

            var result = await pipeline.RunAsync(CancellationToken.None);

            Report(statusStore, result.Awaiting > 0 ? ProcessStateEnum.NeedsAttention : ProcessStateEnum.Ok, Summary(result), null, result.Awaiting);

            return 0;
        }
        catch (Exception exception)
        {
            Log($"Ошибка: {exception}");
            Report(statusStore, ProcessStateEnum.Failed, null, Describe(exception), ideas.LoadAll().Count(idea => idea.State == Ideas.Models.IdeaStateEnum.New));

            return 1;
        }
    }

    /// <summary>
    /// Читает appsettings.json рядом с программой и проверяет токен
    /// </summary>
    /// <returns>Настройки бота</returns>
    /// <exception cref="InvalidOperationException">Файла нет или токен остался заглушкой</exception>
    private static ReelsSettings LoadSettings()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
            throw new InvalidOperationException("Нет файла appsettings.json рядом с программой: скопируйте appsettings.example.json и впишите токен бота");

        var settings = JsonSerializer.Deserialize<ReelsSettings>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidOperationException("Не удалось прочитать appsettings.json");
        if (string.IsNullOrWhiteSpace(settings.BotToken) || settings.BotToken.StartsWith('<'))
            throw new InvalidOperationException("Не задан BotToken в appsettings.json");

        return settings;
    }

    /// <summary>
    /// Собирает строку статуса из итога проверки
    /// </summary>
    /// <param name="result">Итог проверки</param>
    /// <returns>Например «Принято 2, ждут решения 3». Если ничего не пришло, «Новых рилсов нет»</returns>
    private static string Summary(ReelsResult result)
    {
        var parts = new List<string>();
        if (result.Accepted > 0)
            parts.Add($"принято {result.Accepted}");

        if (result.Failed > 0)
            parts.Add($"не получилось {result.Failed}");

        if (result.Awaiting > 0)
            parts.Add($"ждут решения {result.Awaiting}");

        return parts.Count == 0 ? "Новых рилсов нет" : string.Join(", ", parts).Replace("принято", "Принято", StringComparison.Ordinal);
    }

    /// <summary>
    /// Записывает итог в общую папку статусов, откуда его читает приложение в трее
    /// </summary>
    /// <param name="store">Папка статусов</param>
    /// <param name="state">Итоговое состояние</param>
    /// <param name="summary">Сводка или <see langword="null"/> при ошибке</param>
    /// <param name="error">Текст ошибки или <see langword="null"/> при успехе</param>
    /// <param name="awaiting">Сколько идей ждёт решения</param>
    private static void Report(StatusStore store, ProcessStateEnum state, string? summary, string? error, int awaiting)
    {
        try
        {
            store.Report(STATUS_ID, "Рилсы", state, DateTimeOffset.Now, summary, error, success: state != ProcessStateEnum.Failed, awaiting);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Папка статусов недоступна, итог уже записан в вывод
        }
    }

    /// <summary>
    /// Описывает ошибку цепочкой причин: тип и сообщение каждого исключения от внешнего к внутреннему
    /// </summary>
    /// <param name="exception">Исключение проверки</param>
    /// <returns>Строка вида «HttpRequestException: … → …»</returns>
    private static string Describe(Exception exception)
    {
        var parts = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
            parts.Add($"{current.GetType().Name}: {current.Message}");

        return string.Join(" → ", parts);
    }

    /// <summary>
    /// Пишет строку с временем в вывод: приложение показывает каждую такую строку отдельной плашкой
    /// </summary>
    /// <param name="message">Текст строки</param>
    private static void Log(string message) => Console.WriteLine($"{DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture)}  {message}");
}