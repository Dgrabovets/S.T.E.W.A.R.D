using System.Text;
using System.Text.RegularExpressions;
using Steward.Mail.Common;
using Steward.Mail.Interfaces;
using Steward.Mail.Models;
using Steward.Mail.Services;
using Microsoft.Extensions.Configuration;
using Steward.Status;

namespace Steward.Mail;

/// <summary>
/// Проверка учебной почты: задачи в SingularityApp и уведомление со сводкой
/// Запускается приложением в трее: при его старте, утром и вечером
/// </summary>
internal static partial class Program
{
    #region Fields

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
    private const string StatusId = "mail";

    #endregion

    #region Entry point

    /// <summary>
    /// Читает непрочитанные письма, создаёт задачи, пишет итог в лог и в файл статуса и показывает уведомление
    /// </summary>
    /// <param name="args">
    /// Аргументы командной строки. <c>--dry-run</c> только пишет в лог: задачи не создаются, письма не запоминаются, статус не меняется
    /// </param>
    /// <returns>0, если проверка прошла, и 1, если случилась ошибка</returns>
    private static async Task<int> Main(string[] args)
    {
        var dryRun = args.Contains("--dry-run");
        var statusStore = new StatusStore(DataLocation.StatusDirectory());

        try
        {
            var settings = LoadSettings();
            var reader = new ImapMailReader(settings.Mail);
            var classifier = new MailClassifier(settings.Mail, settings.Classifier);
            var state = new JsonStateStore(Path.Combine(BaseDir, "processed.json"));
            using var singularity = new SingularityClient(settings.Singularity);
            INotifier notifier = new ToastNotifier();

            var unread = await reader.GetUnreadAsync(CancellationToken.None);
            var fresh = unread.Where(m => !state.IsProcessed(m.MessageId)).OrderBy(m => m.Date).ToList();

            var feedback = new List<MailItem>();
            var replies = new List<MailItem>();
            var info = new List<MailItem>();

            foreach (var mail in fresh)
            {
                var category = classifier.Classify(mail);
                var task = BuildTask(mail, category, settings);

                if (task is not null && !dryRun)
                {
                    await singularity.CreateTaskAsync(task, CancellationToken.None);
                }

                (category switch
                {
                    MailCategory.TeacherFeedback => feedback,
                    MailCategory.PersonalReply => replies,
                    _ => info
                }).Add(mail);

                Log($"{(dryRun ? "[тест] " : string.Empty)}{CategoryLabel(category)}: {mail.FromName} — {mail.Subject}" +
                    (task is null ? string.Empty : $" → задача «{task.Title}»"));

                if (!dryRun)
                {
                    state.MarkProcessed(mail.MessageId);
                }
            }

            if (!dryRun)
            {
                state.Save();
            }

            var summary = BuildSummary(feedback, replies, info);
            Log(summary.Replace('\n', ' '));

            if (!dryRun)
            {
                ReportStatus(statusStore, ProcessState.Ok, summary.Replace('\n', ' '), null);
            }

            if (feedback.Count + replies.Count > 0)
            {
                notifier.Show("Учебная почта", summary);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Log($"Ошибка: {ex.Message}");
            if (!dryRun)
            {
                ReportStatus(statusStore, ProcessState.Failed, null, ex.Message);
            }

            TryNotifyError(ex.Message);
            return 1;
        }
    }

    #endregion

    #region Private methods

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
        {
            throw new InvalidOperationException("Не задан пароль приложения в Mail:AppPassword");
        }

        if (string.IsNullOrWhiteSpace(settings.Singularity.Token) || settings.Singularity.Token.StartsWith('<'))
        {
            throw new InvalidOperationException("Не задан токен в Singularity:Token");
        }

        return settings;
    }

    /// <summary>
    /// Собирает задачу для SingularityApp
    /// Для информационных писем задача не создаётся
    /// </summary>
    /// <param name="mail">Письмо из почтового ящика</param>
    /// <param name="category">Категория письма</param>
    /// <param name="settings">Настройки: из них берутся проект назначения и адрес веб-почты</param>
    /// <returns>
    /// Задача с приоритетом по категории или <see langword="null"/> для информационного письма
    /// </returns>
    private static SingularityTaskRequest? BuildTask(MailItem mail, MailCategory category, AppSettings settings)
    {
        var note = new StringBuilder()
            .AppendLine($"От: {mail.FromName} <{mail.FromAddress}>")
            .AppendLine($"Тема: {mail.Subject}")
            .AppendLine($"Получено: {mail.Date.LocalDateTime:dd.MM.yyyy HH:mm}")
            .AppendLine()
            .AppendLine(Truncate(mail.Snippet, 500))
            .AppendLine()
            .AppendLine(settings.Mail.WebMailUrl)
            .ToString();

        var today = DateTime.Today.ToString("yyyy-MM-dd");

        return category switch
        {
            MailCategory.TeacherFeedback => new SingularityTaskRequest
            {
                Title = FeedbackTitle(mail.Subject),
                Note = note,
                Priority = 1,
                Start = today,
                ProjectId = settings.Singularity.ProjectId
            },
            MailCategory.PersonalReply => new SingularityTaskRequest
            {
                Title = $"Пришёл ответ от {ShortName(mail.FromName)}, нужно ответить: «{Truncate(mail.Subject, 80)}»",
                Note = note,
                Priority = 0,
                Start = today,
                ProjectId = settings.Singularity.ProjectId
            },
            _ => null
        };
    }

    /// <summary>
    /// «Пользователь Иванов Иван Иванович дал отзыв на Ваш ответ на задание «Задание 2».»
    /// превращается в «Отзыв преподавателя (Иванов И. И.): «Задание 2» — посмотреть»
    /// </summary>
    /// <param name="subject">Тема письма об отзыве</param>
    /// <returns>Название задачи. Если тема не похожа на отзыв, в нём остаётся сама тема</returns>
    private static string FeedbackTitle(string subject)
    {
        var match = FeedbackRegex().Match(subject);
        if (!match.Success)
        {
            return $"Отзыв преподавателя — посмотреть: {Truncate(subject, 80)}";
        }

        return $"Отзыв преподавателя ({ShortName(match.Groups["name"].Value)}): «{match.Groups["task"].Value}» — посмотреть";
    }

    /// <summary>
    /// Сокращает «Иванов Иван Иванович» до «Иванов И. И.»
    /// </summary>
    /// <param name="fullName">Имя отправителя или адрес</param>
    /// <returns>Фамилия и инициалы. Адрес и имя из одного слова остаются как есть</returns>
    private static string ShortName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || fullName.Contains('@'))
        {
            return fullName;
        }

        return parts[0] + " " + string.Join(" ", parts.Skip(1).Take(2).Select(p => p[0] + "."));
    }

    /// <summary>
    /// Собирает текст уведомления и строку статуса: сколько писем требуют ответа, сколько отзывов и сколько прочих
    /// </summary>
    /// <param name="feedback">Письма с отзывами преподавателей</param>
    /// <param name="replies">Письма, на которые нужно ответить</param>
    /// <param name="info">Прочие письма</param>
    /// <returns>Строки по категориям или «Новых важных писем нет», если писем не было</returns>
    private static string BuildSummary(List<MailItem> feedback, List<MailItem> replies, List<MailItem> info)
    {
        if (feedback.Count + replies.Count + info.Count == 0)
        {
            return "Новых важных писем нет";
        }

        var sb = new StringBuilder();
        if (replies.Count > 0)
        {
            sb.AppendLine($"Ответить: {replies.Count} ({string.Join(", ", replies.Select(r => ShortName(r.FromName)).Distinct())})");
        }

        if (feedback.Count > 0)
        {
            sb.AppendLine($"Отзывы и оценки: {feedback.Count}");
        }

        if (info.Count > 0)
        {
            sb.AppendLine($"Прочее: {info.Count}");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Короткая подпись категории для строки лога
    /// </summary>
    /// <param name="category">Категория письма</param>
    /// <returns>«Отзыв», «Ответить» или «Инфо»</returns>
    private static string CategoryLabel(MailCategory category) => category switch
    {
        MailCategory.TeacherFeedback => "Отзыв",
        MailCategory.PersonalReply => "Ответить",
        _ => "Инфо"
    };

    /// <summary>
    /// Обрезает строку до заданной длины
    /// </summary>
    /// <param name="value">Исходный текст</param>
    /// <param name="max">Наибольшая длина без многоточия</param>
    /// <returns>Тот же текст или его начало с многоточием</returns>
    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max].TrimEnd() + "…";

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
    private static void ReportStatus(StatusStore store, ProcessState state, string? summary, string? error)
    {
        try
        {
            var previous = store.Read(StatusId);
            var now = DateTimeOffset.Now;
            store.Write(new ProcessStatus
            {
                Id = StatusId,
                Title = "Почта",
                State = state,
                UpdatedAt = now,
                LastRunAt = now,
                LastSuccessAt = state == ProcessState.Ok ? now : previous?.LastSuccessAt,
                Summary = summary,
                Error = error
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Папка статусов недоступна, итог уже записан в лог
        }
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

    /// <summary>
    /// Разбирает тему письма об отзыве: имя преподавателя и название задания
    /// </summary>
    /// <returns>
    /// Регулярное выражение с группами <c>name</c> и <c>task</c>
    /// </returns>
    [GeneratedRegex("Пользователь\\s+(?<name>.+?)\\s+дал\\s+отзыв.*?«(?<task>[^»]+)»")]
    private static partial Regex FeedbackRegex();

    #endregion
}
