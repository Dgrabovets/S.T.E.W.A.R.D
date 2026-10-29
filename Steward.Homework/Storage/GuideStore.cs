using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Steward.Homework.Models;
using Steward.Homework.Services;

namespace Steward.Homework.Storage;

/// <summary>
/// Методические указания и образцы работ по предметам. Файлы копируются в <c>guides</c> рядом с остальными данными, а файлы и папки по пути на диске и ссылки в интернете запоминаются как ссылки: оригиналы не перемещаются
/// </summary>
/// <param name="directory">Папка домашних заданий, например <c>data/homework</c></param>
public sealed partial class GuideStore(string directory)
{
    /// <summary>
    /// Больше стольких файлов из одной папки агенту не передаётся
    /// </summary>
    private const int FOLDER_LIMIT = 30;

    /// <summary>
    /// Начало ключа записи образца из сданной работы
    /// </summary>
    private const string SAMPLE_PREFIX = "sample:";

    /// <summary>
    /// Сколько последних автоматических образцов предмета берётся в материалы
    /// </summary>
    private const int SAMPLE_LIMIT = 2;

    /// <summary>
    /// Самый большой файл, который скачивается по ссылке, в байтах
    /// </summary>
    private const long MAX_DOWNLOAD_BYTES = 100L * 1024 * 1024;

    /// <summary>
    /// Читает и пишет JSON в camelCase с отступами, кириллица остаётся буквами
    /// </summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Расширения, которые агент читает сам: текст, Markdown, PDF и код
    /// </summary>
    private static readonly HashSet<string> ReadableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".markdown", ".pdf", ".json", ".xml", ".csv", ".cs", ".py", ".js", ".ts", ".java", ".cpp", ".c", ".h", ".sql", ".html", ".css", ".yml", ".yaml", ".rtf",
    };

    /// <summary>
    /// Папка материалов
    /// </summary>
    public string Folder => Path.Combine(directory, "guides");

    /// <summary>
    /// Папка образцов из сданных работ: <c>samples</c> рядом с <c>guides</c>
    /// </summary>
    public string SamplesFolder => Path.Combine(directory, "samples");

    /// <summary>
    /// Файл со списком материалов
    /// </summary>
    private string ListFile => Path.Combine(Folder, "guides.json");

    /// <summary>
    /// Читает все материалы
    /// </summary>
    /// <returns>Материалы в порядке добавления. Пустой список, если файла нет или он испорчен</returns>
    public IReadOnlyList<GuideEntry> Load()
    {
        try
        {
            return File.Exists(ListFile) ? JsonSerializer.Deserialize<List<GuideEntry>>(File.ReadAllText(ListFile), Json) ?? [] : [];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Материалы для подготовки заданий. Они общие для всех предметов: один набор методических указаний и образцов подходит к любому предмету
    /// </summary>
    /// <param name="subject">Название предмета. Не влияет на выбор, оставлено для совместимости</param>
    /// <returns>Все материалы</returns>
    public IReadOnlyList<GuideEntry> ForSubject(string subject) => Load();

    /// <summary>
    /// Копирует файл в папку материалов предмета и запоминает его
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <param name="kind">Указания или образец</param>
    /// <param name="sourcePath">Файл, который выбрал пользователь</param>
    /// <returns>Новая запись</returns>
    /// <exception cref="FileNotFoundException">Файла нет</exception>
    public GuideEntry AddFile(string subject, GuideKindEnum kind, string sourcePath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Файл не найден", sourcePath);

        var folder = SubjectFolder(subject);
        Directory.CreateDirectory(folder);

        var target = UniquePath(folder, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, target);

        return Save(new GuideEntry(NewId(), subject, kind, Path.GetFileName(target), target, string.Empty, string.Empty));
    }

    /// <summary>
    /// Запоминает ссылку: путь к файлу или папке на диске остаётся на месте, адрес в интернете ждёт скачивания
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <param name="kind">Указания или образец</param>
    /// <param name="pathOrUrl">Путь на диске или адрес в интернете</param>
    /// <returns>Новая запись</returns>
    /// <exception cref="ArgumentException">Это не путь к существующему файлу или папке и не адрес в интернете</exception>
    public GuideEntry AddReference(string subject, GuideKindEnum kind, string pathOrUrl)
    {
        var text = pathOrUrl.Trim().Trim('"');
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            return Save(new GuideEntry(NewId(), subject, kind, uri.Host + uri.AbsolutePath.TrimEnd('/'), string.Empty, text, "Ссылка ещё не скачана"));

        if (File.Exists(text) || Directory.Exists(text))
            return Save(new GuideEntry(NewId(), subject, kind, Path.GetFileName(text.TrimEnd('\\', '/')) is { Length: > 0 } name ? name : text, Path.GetFullPath(text), string.Empty, string.Empty));

        throw new ArgumentException("Это не путь к существующему файлу или папке и не адрес в интернете", nameof(pathOrUrl));
    }

    /// <summary>
    /// Копирует сданную работу в <c>samples/&lt;безопасное имя предмета&gt;</c> и регистрирует её образцом предмета с ключом <c>sample:&lt;занятие&gt;:&lt;номер&gt;</c>. Повтор для того же задания заменяет запись и файл. Файл владельца не заменяется черновиком
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <param name="file">Файл работы</param>
    /// <param name="fromUser">Работу принёс владелец, а не взят готовый черновик</param>
    /// <returns>Запись образца. Если у задания уже образец владельца, а пришёл черновик, возвращается прежняя запись</returns>
    /// <exception cref="FileNotFoundException">Файла нет</exception>
    public GuideEntry AddSample(string subject, string lessonId, int number, string file, bool fromUser)
    {
        GuideEntry result = null!;
        Locked(() => result = AddSampleCore(subject, lessonId, number, file, fromUser));

        return result;
    }

    /// <summary>
    /// Регистрация образца без блокировки: вызывается под ней
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <param name="file">Файл работы</param>
    /// <param name="fromUser">Работу принёс владелец</param>
    /// <returns>Запись образца</returns>
    private GuideEntry AddSampleCore(string subject, string lessonId, int number, string file, bool fromUser)
    {
        if (!File.Exists(file))
            throw new FileNotFoundException("Файл не найден", file);

        var id = SampleId(lessonId, number);
        var previous = Load().FirstOrDefault(entry => entry.Id == id);
        var previousExists = previous is { Path.Length: > 0 } && File.Exists(previous.Path);
        if (previous is { Manual: true } && previousExists && !fromUser)
            return previous;

        var folder = Path.Combine(SamplesFolder, RemarkStore.SafeName(subject));
        var extension = new string([.. Path.GetExtension(file).Where(symbol => char.IsAsciiLetterOrDigit(symbol) || symbol == '.').Take(16)]);
        var target = Path.GetFullPath(Path.Combine(folder, $"{RemarkStore.SafeName(lessonId)}-{number}{extension}"));
        if (!IsInside(target, SamplesFolder))
            throw new ArgumentException("Имя образца выходит за пределы папки образцов", nameof(lessonId));

        // Файл не менялся, если у прежней записи тот же размер и тот же хеш содержимого: тогда дата добавления остаётся прежней
        var unchanged = previous is not null && previousExists && SameContent(previous.Path, file);

        Directory.CreateDirectory(folder);
        var partial = target + ".partial";
        try
        {
            File.Copy(file, partial, overwrite: true);
            File.Move(partial, target, overwrite: true);
        }
        catch
        {
            File.Delete(partial);

            throw;
        }

        var entry = new GuideEntry(id, subject, GuideKindEnum.Example, Path.GetFileName(target), target, string.Empty, string.Empty, Enabled: previous?.Enabled ?? true, Manual: fromUser, Added: unchanged ? previous!.Added : DateTime.UtcNow);
        Write([.. Load().Where(item => item.Id != id), entry]);

        // Прежний файл с другим расширением больше не нужен
        if (previous is not null && previous.Path.Length > 0 && !string.Equals(Path.GetFullPath(previous.Path), target, StringComparison.OrdinalIgnoreCase) && IsInside(previous.Path, SamplesFolder) && File.Exists(previous.Path))
        {
            try
            {
                File.Delete(previous.Path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Запись уже заменена: лишний файл останется на диске, но образцом не станет
            }
        }

        return entry;
    }

    /// <summary>
    /// Скачивает файл по ссылке в папку материалов предмета. Ссылки Google Диска и Google Документов превращаются в прямые. Если по ссылке открывается страница, а не файл, запись остаётся с пояснением
    /// </summary>
    /// <param name="entry">Запись со ссылкой</param>
    /// <param name="http">Клиент для запроса</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Обновлённая запись</returns>
    public async Task<GuideEntry> DownloadAsync(GuideEntry entry, HttpClient http, CancellationToken cancellationToken)
    {
        if (entry.Url.Length == 0)
            return entry;

        try
        {
            using var response = await http.GetAsync(Direct(entry.Url), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Replace(entry with { Note = $"Не скачалось: сервер ответил {(int)response.StatusCode}" });

            if (response.Content.Headers.ContentType?.MediaType is "text/html")
                return Replace(entry with { Note = "Ссылка ведёт на страницу, а не на файл. Скачай файл и добавь его кнопкой «Файл»" });

            var name = FileName(response.Content.Headers.ContentDisposition, entry.Url, response.Content.Headers.ContentType?.MediaType);
            var folder = SubjectFolder(entry.Subject);
            Directory.CreateDirectory(folder);

            if (response.Content.Headers.ContentLength > MAX_DOWNLOAD_BYTES)
                return Replace(entry with { Note = "Не скачалось: файл больше 100 МБ" });

            var target = UniquePath(folder, name);
            try
            {
                await using (var output = File.Create(target))
                {
                    await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
                    var copied = await CopyLimitedAsync(body, output, cancellationToken);
                    if (!copied)
                        throw new IOException("файл больше 100 МБ");
                }
            }
            catch
            {
                File.Delete(target);

                throw;
            }

            return Replace(entry with { Title = Path.GetFileName(target), Path = target, Note = string.Empty });
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return Replace(entry with { Note = "Не скачалось: " + exception.Message });
        }
    }

    /// <summary>
    /// Копирует поток в файл, пока не набралось больше предела размера
    /// </summary>
    /// <param name="source">Откуда читать</param>
    /// <param name="target">Куда писать</param>
    /// <param name="cancellationToken">Отменяет копирование</param>
    /// <returns>
    /// <see langword="false"/>, если данных оказалось больше предела
    /// </returns>
    private static async Task<bool> CopyLimitedAsync(Stream source, Stream target, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > MAX_DOWNLOAD_BYTES)
                return false;

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Включает или выключает материал: выключенный остаётся в списке, но при подготовке заданий не учитывается
    /// </summary>
    /// <param name="id">Ключ записи</param>
    /// <param name="enabled">Учитывать материал</param>
    public void SetEnabled(string id, bool enabled) => Locked(() => Write([.. Load().Select(item => item.Id == id ? item with { Enabled = enabled } : item)]));

    /// <summary>
    /// Убирает материал из списка. Файл, который приложение скопировало к себе, удаляется, оригиналы по ссылке не трогаются
    /// </summary>
    /// <param name="id">Ключ записи</param>
    public void Remove(string id) => Locked(() =>
    {
        var all = Load();
        var entry = all.FirstOrDefault(item => item.Id == id);
        if (entry is null)
            return;

        Write([.. all.Where(item => item.Id != id)]);
        if (entry.Path.Length > 0 && File.Exists(entry.Path) && (IsInside(entry.Path, Folder) || IsInside(entry.Path, SamplesFolder)))
            File.Delete(entry.Path);
    });

    /// <summary>
    /// Выполняет изменение списка под блокировкой: общей для потоков процесса и именованным мьютексом для процессов (приложение и Steward.Lms)
    /// </summary>
    /// <param name="action">Действие, которое читает и пишет список материалов</param>
    /// <exception cref="IOException">Список занят другим процессом</exception>
    private void Locked(Action action) => FileLock.Run(directory, "Steward.Guides.", action);

    /// <summary>
    /// Совпадает ли содержимое двух файлов: сначала сравнивается размер, потом хеш
    /// </summary>
    /// <param name="first">Первый файл</param>
    /// <param name="second">Второй файл</param>
    /// <returns>
    /// <see langword="true"/>, если файлы читаются и содержимое одинаково
    /// </returns>
    private static bool SameContent(string first, string second)
    {
        try
        {
            if (new FileInfo(first).Length != new FileInfo(second).Length)
                return false;

            using var one = File.OpenRead(first);
            using var two = File.OpenRead(second);

            return SHA256.HashData(one).AsSpan().SequenceEqual(SHA256.HashData(two));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Лежит ли путь внутри папки (сама папка не считается)
    /// </summary>
    /// <param name="path">Проверяемый путь</param>
    /// <param name="folder">Папка</param>
    /// <returns>
    /// <see langword="true"/>, если путь строго внутри папки
    /// </returns>
    private static bool IsInside(string path, string folder) =>
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Ключ записи образца
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <returns>Строка вида <c>sample:&lt;занятие&gt;:&lt;номер&gt;</c></returns>
    private static string SampleId(string lessonId, int number) => $"{SAMPLE_PREFIX}{lessonId}:{number}";

    /// <summary>
    /// Собирает материалы предмета для агента: читаемые файлы указаний и образцов и папки, к которым агенту нужен доступ
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <param name="ids">Какие материалы взять. <see langword="null"/>: все включённые</param>
    /// <returns>Файлы по видам и папки</returns>
    public GuideMaterials Materials(string subject, IReadOnlySet<string>? ids = null)
    {
        var chosen = ForSubject(subject)
            .Where(entry => ids is not null ? ids.Contains(entry.Id) : entry.Enabled)
            .Where(entry => entry.Path.Length > 0 && (File.Exists(entry.Path) || Directory.Exists(entry.Path)))
            .ToArray();

        // Образцы из сданных работ только этого предмета: все ручные и два последних автоматических (при явном выборе берутся все выбранные)
        var samples = chosen.Where(entry => entry.Id.StartsWith(SAMPLE_PREFIX, StringComparison.Ordinal) && string.Equals(entry.Subject, subject, StringComparison.OrdinalIgnoreCase)).ToArray();
        var latest = samples.Where(entry => !entry.Manual)
            .Select((entry, index) => (entry, index))
            .OrderByDescending(pair => pair.entry.Added ?? DateTime.MinValue).ThenByDescending(pair => pair.index)
            .Select(pair => pair.entry)
            .Take(ids is null ? SAMPLE_LIMIT : int.MaxValue);
        var entries = chosen.Where(entry => !entry.Id.StartsWith(SAMPLE_PREFIX, StringComparison.Ordinal)).Concat(samples.Where(entry => entry.Manual)).Concat(latest).ToArray();
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<string> Files(GuideKindEnum kind)
        {
            var files = new List<string>();
            foreach (var entry in entries.Where(item => item.Kind == kind))
            {
                var sources = Directory.Exists(entry.Path) ? Directory.EnumerateFiles(entry.Path, "*", SearchOption.AllDirectories).Take(FOLDER_LIMIT).ToArray() : [entry.Path];
                foreach (var source in sources)
                {
                    if (Readable(source) is { } readable)
                    {
                        files.Add(readable);
                        directories.Add(Path.GetDirectoryName(source)!);
                    }
                }
            }

            return files;
        }

        var guides = Files(GuideKindEnum.Guide);
        var examples = Files(GuideKindEnum.Example);

        return new GuideMaterials(guides, examples, [.. directories]);
    }

    /// <summary>
    /// Путь к файлу, который агент может прочитать: сам файл для текста и PDF, а для Word и презентаций текстовая копия с оформлением в папке <c>guides/.text</c>
    /// </summary>
    /// <param name="source">Исходный файл</param>
    /// <returns>Путь или <see langword="null"/>, если формат агенту не прочитать</returns>
    public string? Readable(string source)
    {
        var extension = Path.GetExtension(source);
        if (ReadableExtensions.Contains(extension))
            return source;

        if (!extension.Equals(".docx", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase))
            return null;

        var cache = Path.Combine(Folder, ".text", Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(source).ToLowerInvariant())))[..12] + "-" + Path.GetFileNameWithoutExtension(source) + ".txt");
        if (File.Exists(cache) && File.GetLastWriteTimeUtc(cache) >= File.GetLastWriteTimeUtc(source))
            return cache;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
            File.WriteAllText(cache, extension.Equals(".docx", StringComparison.OrdinalIgnoreCase) ? OfficeText.Docx(source) : OfficeText.Pptx(source));

            return cache;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return null;
        }
    }

    /// <summary>
    /// Превращает ссылки Google Диска и Документов в прямые ссылки на скачивание
    /// </summary>
    /// <param name="url">Ссылка, которую вставил пользователь</param>
    /// <returns>Адрес, по которому отдаётся файл</returns>
    public static string Direct(string url)
    {
        var drive = DriveFile().Match(url);
        if (drive.Success)
            return $"https://drive.google.com/uc?export=download&id={drive.Groups[1].Value}";

        var doc = GoogleDoc().Match(url);
        if (doc.Success)
        {
            var format = doc.Groups[1].Value switch { "presentation" => "pptx", "spreadsheets" => "xlsx", _ => "docx" };

            return $"https://docs.google.com/{doc.Groups[1].Value}/d/{doc.Groups[2].Value}/export?format={format}";
        }

        return url;
    }

    /// <summary>
    /// Выбирает имя файла: из заголовка ответа, иначе из конца адреса, иначе по типу содержимого
    /// </summary>
    /// <param name="disposition">Заголовок с именем файла</param>
    /// <param name="url">Адрес</param>
    /// <param name="mediaType">Тип содержимого</param>
    /// <returns>Имя файла без запрещённых знаков</returns>
    private static string FileName(ContentDispositionHeaderValue? disposition, string url, string? mediaType)
    {
        var name = (disposition?.FileNameStar ?? disposition?.FileName ?? string.Empty).Trim('"');
        if (name.Length == 0 && Uri.TryCreate(url, UriKind.Absolute, out var uri))
            name = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath));

        var clean = string.Concat(name.Where(symbol => !Path.GetInvalidFileNameChars().Contains(symbol))).Trim();
        if (clean.Length == 0)
            clean = "материал";

        if (Path.GetExtension(clean).Length == 0)
        {
            clean += mediaType switch
            {
                "application/pdf" => ".pdf",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
                "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
                "text/plain" => ".txt",
                _ => ".bin",
            };
        }

        return clean;
    }

    /// <summary>
    /// Папка материалов одного предмета
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <returns>Путь внутри <c>guides</c></returns>
    private string SubjectFolder(string subject)
    {
        var slug = string.Concat(subject.Where(symbol => char.IsLetterOrDigit(symbol) || symbol is ' ' or '-')).Trim();

        return Path.Combine(Folder, slug.Length > 0 ? slug : "Общие");
    }

    /// <summary>
    /// Подбирает имя, которого ещё нет в папке: к занятому добавляется номер
    /// </summary>
    /// <param name="folder">Папка</param>
    /// <param name="name">Желаемое имя файла</param>
    /// <returns>Свободный путь</returns>
    private static string UniquePath(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        for (var index = 2; File.Exists(path); index++)
            path = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(name)} ({index}){Path.GetExtension(name)}");

        return path;
    }

    /// <summary>
    /// Новый короткий ключ записи
    /// </summary>
    /// <returns>Восемь знаков</returns>
    private static string NewId() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Добавляет запись в список
    /// </summary>
    /// <param name="entry">Запись</param>
    /// <returns>Та же запись</returns>
    private GuideEntry Save(GuideEntry entry)
    {
        Locked(() => Write([.. Load(), entry]));

        return entry;
    }

    /// <summary>
    /// Заменяет запись с тем же ключом
    /// </summary>
    /// <param name="entry">Новое состояние записи</param>
    /// <returns>Та же запись</returns>
    private GuideEntry Replace(GuideEntry entry)
    {
        Locked(() => Write([.. Load().Select(item => item.Id == entry.Id ? entry : item)]));

        return entry;
    }

    /// <summary>
    /// Записывает список через временный файл
    /// </summary>
    /// <param name="entries">Все записи</param>
    private void Write(IReadOnlyList<GuideEntry> entries)
    {
        Directory.CreateDirectory(Folder);

        var temp = ListFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(entries, Json));
            File.Move(temp, ListFile, overwrite: true);
        }
        catch
        {
            File.Delete(temp);

            throw;
        }
    }

    [GeneratedRegex(@"drive\.google\.com/file/d/([\w-]+)")]
    private static partial Regex DriveFile();

    [GeneratedRegex(@"docs\.google\.com/(document|presentation|spreadsheets)/d/([\w-]+)")]
    private static partial Regex GoogleDoc();
}

/// <summary>
/// Материалы предмета, подготовленные для агента
/// </summary>
/// <param name="Guides">Читаемые файлы методических указаний</param>
/// <param name="Examples">Читаемые файлы образцов</param>
/// <param name="Directories">Папки, к которым агенту нужен доступ на чтение</param>
public sealed record GuideMaterials(IReadOnlyList<string> Guides, IReadOnlyList<string> Examples, IReadOnlyList<string> Directories)
{
    /// <summary>
    /// Материалов нет
    /// </summary>
    public static GuideMaterials Empty { get; } = new([], [], []);

    /// <summary>
    /// Есть хотя бы один файл
    /// </summary>
    public bool Any => Guides.Count + Examples.Count > 0;
}
