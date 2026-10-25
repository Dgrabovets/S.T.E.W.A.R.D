using Microsoft.Extensions.Configuration;
using Steward.Mail.Common;
using Steward.Mail.Services;
using Steward.Status.Common;
using Steward.Status.State;

namespace Steward.Mail;

/// <summary>
/// Проверка учебной почты: задачи в SingularityApp и уведомление со сводкой
/// Запускается приложением в трее: при его старте, утром и вечером
/// </summary>
internal static class Program
{
    /// <summary>
    /// Каталог программы: рядом с ним лежат настройки, список обработанных писем и лог
    /// </summary>
    private static readonly string BaseDir = AppContext.BaseDirectory;

    /// <summary>
    /// Лог проверок: по строке на письмо и на итог
    /// </summary>
    private static readonly string LogPath = Path.Combine(BaseDir, "mail-check.log");

    /// <summary>
    /// Идентификатор процесса в общем формате статусов, он же имя файла и id задачи в config/jobs.json
    /// </summary>
    private const string STATUS_ID = "mail";

    #region Entry point

    /// <summary>
    /// Читает непрочитанные письма, создаёт задачи, пишет итог в лог и в файл статуса и показывает уведомление
    /// </summary>
    /// <param name="args">
    /// Аргументы командной строки. <c>--dry-run</c> только пишет в лог: задачи не создаются, статус не меняется
    /// </param>
    /// <returns>0, если проверка прошла, и 1, если случилась ошибка</returns>
    private static async Task<int> Main(string[] args)
    {
        var dryRun = args.Contains("--dry-run");
        var statusStore = new StatusStore(DataLocation.StatusDirectory());

        try
        {
            var settings = LoadSettings();
            using var singularity = new SingularityClient(settings.Singularity);
            var checker = new MailChecker(
                new ImapMailReader(settings.Mail),
                new MailClassifier(settings.Mail, settings.Classifier),
                singularity,
                new ToastNotifier(),
                settings,
                TimeProvider.System,
                Log);

            var result = await checker.RunAsync(dryRun, CancellationToken.None);

            if (!dryRun)
                ReportStatus(statusStore, ProcessStateEnum.Ok, result.Summary.Replace('\n', ' '), null);

            return 0;
        }
        catch (Exception exception)
        {
            // В лог идёт полный текст исключения со стеком, в статус цепочка причин, в уведомление коротко
            Log($"Ошибка: {exception}");
            if (!dryRun)
                ReportStatus(statusStore, ProcessStateEnum.Failed, null, Describe(exception));

            TryNotifyError(exception.Message);
            return 1;
        }
    }

    #endregion Entry point

    #region Helpers

    /// <summary>
    /// Читает appsettings.json рядом с exe и проверяет, что пароль и токен не остались заглушками
    /// </summary>
    /// <returns>Настройки приложения</returns>
    /// <exception cref="InvalidOperationException">
    /// Файл не читается, либо не заданы пароль приложения или токен SingularityApp
    /// </exception>
    private static AppSettings LoadSettings()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(BaseDir)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var settings = config.Get<AppSettings>() ?? throw new InvalidOperationException("Не удалось прочитать appsettings.json");

        if (string.IsNullOrWhiteSpace(settings.Mail.AppPassword) || settings.Mail.AppPassword.StartsWith('<'))
            throw new InvalidOperationException("Не задан пароль приложения в Mail:AppPassword");

        if (string.IsNullOrWhiteSpace(settings.Singularity.Token) || settings.Singularity.Token.StartsWith('<'))
            throw new InvalidOperationException("Не задан токен в Singularity:Token");

        return settings;
    }

    /// <summary>
    /// Записывает итог проверки в общую папку статусов, откуда его читает приложение в трее
    /// Сбой записи не мешает проверке: статус нужен только для показа
    /// </summary>
    /// <param name="store">Папка статусов</param>
    /// <param name="state">Итоговое состояние: успех или ошибка</param>
    /// <param name="summary">
    /// Сводка проверки одной строкой или <see langword="null"/> при ошибке
    /// </param>
    /// <param name="error">
    /// Текст ошибки или <see langword="null"/> при успехе
    /// </param>
    private static void ReportStatus(StatusStore store, ProcessStateEnum state, string? summary, string? error)
    {
        try
        {
            store.Report(STATUS_ID, "Почта", state, DateTimeOffset.Now, summary, error, success: state == ProcessStateEnum.Ok);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Папка статусов недоступна, итог уже записан в лог
        }
    }

    /// <summary>
    /// Описывает ошибку цепочкой причин: тип и сообщение каждого исключения от внешнего к внутреннему
    /// </summary>
    /// <param name="exception">Исключение проверки</param>
    /// <returns>Строка вида «HttpRequestException: ответил 400 → SocketException: ...»</returns>
    private static string Describe(Exception exception)
    {
        var parts = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
            parts.Add($"{current.GetType().Name}: {current.Message}");

        return string.Join(" → ", parts);
    }

    /// <summary>
    /// Пишет строку с временем в консоль и в лог
    /// </summary>
    /// <param name="message">Текст строки</param>
    private static void Log(string message)
    {
        var line = $"{DateTime.Now:dd.MM.yyyy HH:mm:ss}  {message}";
        Console.WriteLine(line);
        File.AppendAllText(LogPath, line + Environment.NewLine);
    }

    /// <summary>
    /// Показывает уведомление об ошибке. Если уведомления недоступны, молчит: ошибка уже записана в лог
    /// </summary>
    /// <param name="message">Текст ошибки</param>
    private static void TryNotifyError(string message)
    {
        try
        {
            new ToastNotifier().Show("Проверка почты не удалась", message);
        }
        catch
        {
            // Уведомления недоступны, ошибка уже записана в лог
        }
    }

    #endregion Helpers
}
