using System.Globalization;
using System.Text;
using System.Web;
using Steward.Homework.Services;
using Steward.Homework.Storage;
using Steward.Lms.Models;

namespace Steward.Lms.Services;

/// <summary>
/// Выгружает из LMS описания несданных заданий и прикреплённые к ним файлы в <c>data/homework/assignments/&lt;номер&gt;</c>. Условие задания агент читает при поиске и подготовке работ
/// </summary>
public static class AssignmentSync
{
    /// <summary>
    /// Самый большой вложенный файл в байтах
    /// </summary>
    private const long MAX_FILE_BYTES = 30L * 1024 * 1024;

    /// <summary>
    /// Больше стольких вложений к одному заданию не качается
    /// </summary>
    private const int MAX_FILES = 6;

    /// <summary>
    /// Через сколько часов страница задания читается заново, чтобы заметить правки преподавателя
    /// </summary>
    private const int RECHECK_HOURS = 6;

    /// <summary>
    /// Отпечаток условия: хэш текста, дат и списка файлов. Если он изменился, преподаватель поправил задание
    /// </summary>
    /// <param name="details">Что написано на странице</param>
    /// <returns>Шестнадцатеричный хэш</returns>
    public static string Fingerprint(AssignmentDetails details)
    {
        var text = string.Join('\n', [details.Text, .. details.Dates, .. details.Files.Select(file => file.Name + "|" + file.Url)]);

        return System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>
    /// Собирает текст файла <c>assignment.md</c>: название, предмет, срок и условие, а под ним список файлов
    /// </summary>
    /// <param name="activity">Работа из LMS</param>
    /// <param name="details">Что написано на странице</param>
    /// <param name="saved">Скачанные файлы: имя и путь на диске</param>
    /// <returns>Текст Markdown</returns>
    public static string Describe(LmsActivity activity, AssignmentDetails details, IReadOnlyList<(string Name, string Path)> saved)
    {
        var text = new StringBuilder();
        text.AppendLine($"# {activity.Name}").AppendLine();
        text.AppendLine($"Предмет: {activity.Subject}");
        if (activity.Section.Length > 0)
            text.AppendLine($"Раздел: {activity.Section}");

        if (activity.Due is { } due)
            text.AppendLine($"Срок сдачи: {due.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}");

        text.AppendLine($"Состояние: {activity.Status}");
        foreach (var date in details.Dates)
            text.AppendLine(date);

        text.AppendLine($"Страница в LMS: {activity.Url}").AppendLine();
        text.AppendLine("## Условие").AppendLine();
        text.AppendLine(details.Text.Length > 0 ? details.Text : "Текст условия на странице не указан, смотри прикреплённые файлы.").AppendLine();
        if (saved.Count > 0)
        {
            text.AppendLine("## Прикреплённые файлы").AppendLine();
            foreach (var (name, path) in saved)
                text.AppendLine($"- {name}: {path}");
        }
        else if (details.Files.Count > 0)
        {
            text.AppendLine("## Прикреплённые файлы").AppendLine().AppendLine("Файлы есть, но скачать их не удалось: " + string.Join(", ", details.Files.Select(file => file.Name)));
        }

        return text.ToString();
    }

    /// <summary>
    /// Выгружает описания несданных заданий, у которых их ещё нет на диске
    /// </summary>
    /// <param name="lms">Клиент LMS с выполненным входом</param>
    /// <param name="store">Папка домашних заданий</param>
    /// <param name="activities">Работы из LMS</param>
    /// <param name="log">Принимает строки лога</param>
    /// <returns>Сколько заданий выгружено</returns>
    public static async Task<int> SyncAsync(LmsClient lms, HomeworkStore store, IReadOnlyList<LmsActivity> activities, Action<string> log)
    {
        var done = 0;
        foreach (var activity in activities.Where(item => item.Kind == ActivityKindEnum.Assignment && !item.Done))
        {
            var folder = store.AssignmentDirectory(activity.Id);
            var described = Path.Combine(folder, "assignment.md");
            var checkedFile = Path.Combine(folder, "checked.txt");
            if (File.Exists(described) && File.Exists(checkedFile) && DateTime.UtcNow - File.GetLastWriteTimeUtc(checkedFile) < TimeSpan.FromHours(RECHECK_HOURS))
                continue;

            try
            {
                var details = AssignmentParser.Parse(await lms.PageAsync(activity.Url, CancellationToken.None));
                Directory.CreateDirectory(folder);

                var fingerprintFile = Path.Combine(folder, "fingerprint.txt");
                var fingerprint = Fingerprint(details);
                var previous = File.Exists(fingerprintFile) ? File.ReadAllText(fingerprintFile).Trim() : string.Empty;
                if (File.Exists(described) && (previous.Length == 0 || previous == fingerprint))
                {
                    File.WriteAllText(fingerprintFile, fingerprint);
                    File.WriteAllText(checkedFile, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));

                    continue;
                }

                var saved = new List<(string, string)>();
                foreach (var file in details.Files.Take(MAX_FILES))
                {
                    var name = SafeName(Uri.TryCreate(file.Url, UriKind.Absolute, out var uri) ? HttpUtility.UrlDecode(Path.GetFileName(uri.AbsolutePath)) : file.Name);
                    var path = Path.Combine(folder, name);
                    if (!await lms.DownloadAsync(file.Url, path, MAX_FILE_BYTES, CancellationToken.None))
                        continue;

                    saved.Add((file.Name, path));
                    Readable(path);
                }

                File.WriteAllText(described, Describe(activity, details, saved));
                File.WriteAllText(fingerprintFile, fingerprint);
                File.WriteAllText(checkedFile, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                if (previous.Length > 0)
                {
                    File.WriteAllText(Path.Combine(folder, "changed.txt"), DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    log($"Условие задания изменено в LMS: {activity.Subject}, {activity.Name}");
                }
                else
                {
                    log($"Описание задания: {activity.Subject}, {activity.Name}, файлов: {saved.Count}");
                }

                done++;
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or UnauthorizedAccessException or InvalidDataException)
            {
                log($"Описание задания не выгрузилось: {activity.Name}: {exception.Message}");
            }
        }

        return done;
    }

    /// <summary>
    /// Для Word и презентаций кладёт рядом текстовую копию, которую агент сможет прочитать
    /// </summary>
    /// <param name="path">Скачанный файл</param>
    private static void Readable(string path)
    {
        try
        {
            var extension = Path.GetExtension(path);
            if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
                File.WriteAllText(path + ".txt", OfficeText.Docx(path));
            else if (extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase))
                File.WriteAllText(path + ".txt", OfficeText.Pptx(path));
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or System.Xml.XmlException)
        {
            // Файл не открывается как Office: остаётся как есть
        }
    }

    /// <summary>
    /// Имя файла без запрещённых знаков
    /// </summary>
    /// <param name="name">Имя из адреса</param>
    /// <returns>Безопасное имя</returns>
    private static string SafeName(string name)
    {
        var clean = string.Concat(name.Where(symbol => !Path.GetInvalidFileNameChars().Contains(symbol))).Trim();

        return clean.Length == 0 ? "файл" : clean;
    }
}
