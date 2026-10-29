using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Steward.Homework.Models;

namespace Steward.Homework.Storage;

/// <summary>
/// Замечания владельца: предметные лежат в <c>remarks/&lt;имя предмета&gt;.json</c>, общие в <c>remarks/_all.json</c>
/// </summary>
public sealed partial class RemarkStore
{
    private const string ALL_FILE = "_all";
    private const int MAX_NAME = 60;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static readonly string[] Reserved = ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];

    private readonly string _directory;

    /// <summary>
    /// Создаёт хранилище в папке замечаний
    /// </summary>
    /// <param name="directory">Папка замечаний (создаётся при первой записи)</param>
    public RemarkStore(string directory)
    {
        _directory = directory;
    }

    /// <summary>
    /// Добавляет замечание. Если такой же текст (без учёта регистра и лишних пробелов) уже есть в том же файле, повтор не добавляется
    /// </summary>
    /// <param name="remark">Замечание</param>
    public void Add(Remark remark)
    {
        var path = FileFor(remark);
        var list = Read(path);
        if (list.Any(existing => Normalize(existing.Text) == Normalize(remark.Text)))
            return;

        list.Add(remark);
        Write(path, list);
    }

    /// <summary>
    /// Предметные замечания (без общих)
    /// </summary>
    /// <param name="subject">Предмет</param>
    /// <returns>Замечания предмета</returns>
    public IReadOnlyList<Remark> Load(string subject) => Read(SubjectFile(subject));

    /// <summary>
    /// Все замечания: общие и всех предметов
    /// </summary>
    /// <returns>Все замечания</returns>
    public IReadOnlyList<Remark> LoadAll() => [.. Files().Order(StringComparer.Ordinal).SelectMany(Read)];

    /// <summary>
    /// Заменяет замечание с тем же идентификатором. Если замечания нет, ничего не меняется
    /// </summary>
    /// <param name="remark">Новое содержимое замечания</param>
    public void Update(Remark remark)
    {
        foreach (var path in Files())
        {
            var list = Read(path);
            var index = list.FindIndex(existing => existing.Id == remark.Id);
            if (index < 0)
                continue;

            if (FileFor(remark) == path)
            {
                list[index] = remark;
                Write(path, list);
            }
            else
            {
                // Предмет или признак «общее» изменились: переносим замечание в другой файл
                list.RemoveAt(index);
                Write(path, list);
                Add(remark);
            }

            return;
        }
    }

    /// <summary>
    /// Удаляет замечание по идентификатору
    /// </summary>
    /// <param name="id">Идентификатор</param>
    public void Remove(string id)
    {
        foreach (var path in Files())
        {
            var list = Read(path);
            if (list.RemoveAll(existing => existing.Id == id) > 0)
                Write(path, list);
        }
    }

    /// <summary>
    /// Выбирает замечания для подготовки: включённые, без повторов по тексту, общие и предметные поровну (нехватку одних добирают другими), свежие раньше
    /// </summary>
    /// <param name="subject">Предмет</param>
    /// <param name="kind">Нужный вид или <see langword="null"/> для любого</param>
    /// <param name="limit">Сколько замечаний взять не больше</param>
    /// <returns>Отобранные замечания</returns>
    public IReadOnlyList<Remark> ForPrepare(string subject, RemarkKindEnum? kind = null, int limit = 10)
    {
        if (limit <= 0)
            return [];

        bool Fits(Remark remark) => remark.Enabled && (kind is null || remark.Kind == kind);

        var globalOnes = Distinct(Read(AllFile()).Where(Fits).Reverse());
        var globalTexts = globalOnes.Select(remark => Normalize(remark.Text)).ToHashSet();

        // Предметное замечание с тем же текстом, что и общее, берём один раз: из общих
        var subjectOnes = Distinct(Load(subject).Where(Fits).Reverse()).Where(remark => !globalTexts.Contains(Normalize(remark.Text))).ToList();

        var takeSubject = Math.Min(subjectOnes.Count, Math.Max(limit - limit / 2, limit - globalOnes.Count));
        var takeGlobal = Math.Min(globalOnes.Count, limit - takeSubject);

        return [.. subjectOnes.Take(takeSubject), .. globalOnes.Take(takeGlobal)];
    }

    /// <summary>
    /// Убирает повторы по нормализованному тексту, оставляя первое вхождение
    /// </summary>
    /// <param name="remarks">Замечания</param>
    /// <returns>Замечания без повторов</returns>
    private static List<Remark> Distinct(IEnumerable<Remark> remarks)
    {
        var seen = new HashSet<string>();

        return [.. remarks.Where(remark => seen.Add(Normalize(remark.Text)))];
    }

    /// <summary>
    /// Все файлы замечаний в папке
    /// </summary>
    /// <returns>Пути к файлам</returns>
    private List<string> Files()
    {
        try
        {
            return Directory.Exists(_directory) ? [.. Directory.EnumerateFiles(_directory, "*.json")] : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Файл, где хранится замечание
    /// </summary>
    /// <param name="remark">Замечание</param>
    /// <returns>Путь к файлу</returns>
    private string FileFor(Remark remark) => remark.Global ? AllFile() : SubjectFile(remark.Subject);

    /// <summary>
    /// Файл общих замечаний
    /// </summary>
    /// <returns>Путь к файлу</returns>
    private string AllFile() => Path.Combine(_directory, ALL_FILE + ".json");

    /// <summary>
    /// Файл замечаний предмета
    /// </summary>
    /// <param name="subject">Предмет</param>
    /// <returns>Путь к файлу</returns>
    private string SubjectFile(string subject) => Path.Combine(_directory, SafeName(subject) + ".json");

    /// <summary>
    /// Безопасное имя файла по названию предмета. Если название пришлось менять (запрещённые знаки, зарезервированное имя, слишком длинное или совпадает с файлом общих замечаний), добавляется короткая метка из хеша, чтобы разные предметы не слились
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <returns>Имя файла без расширения</returns>
    public static string SafeName(string subject)
    {
        var clean = Spaces().Replace(string.Concat(subject.Where(symbol => !Path.GetInvalidFileNameChars().Contains(symbol) && !char.IsControl(symbol))), " ").Trim().TrimEnd('.', ' ');
        var changed = clean != subject || clean.Length == 0 || clean.Length > MAX_NAME
            || clean.Equals(ALL_FILE, StringComparison.OrdinalIgnoreCase) || Reserved.Contains(clean.Split('.')[0], StringComparer.OrdinalIgnoreCase);
        if (!changed)
            return clean;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(subject)))[..8].ToLowerInvariant();
        var head = (clean.Length > MAX_NAME ? clean[..MAX_NAME] : clean).TrimEnd('.', ' ');

        return head.Length == 0 ? "предмет-" + hash : head + "-" + hash;
    }

    /// <summary>
    /// Приводит текст к виду для сравнения: без регистра и лишних пробелов
    /// </summary>
    /// <param name="text">Текст</param>
    /// <returns>Нормализованный текст</returns>
    private static string Normalize(string text) => Spaces().Replace(text, " ").Trim().ToLowerInvariant();

    /// <summary>
    /// Читает файл замечаний. Нет файла или он повреждён: пустой список
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    /// <returns>Изменяемый список замечаний</returns>
    private static List<Remark> Read(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<List<Remark>>(File.ReadAllText(path), Json)?.Where(remark => remark?.Text is not null && remark.Id is not null).ToList() ?? [] : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Записывает файл атомарно: сначала во временный файл, потом перенос на место
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    /// <param name="list">Замечания</param>
    private void Write(string path, List<Remark> list)
    {
        Directory.CreateDirectory(_directory);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(list, Json));
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            File.Delete(temp);

            throw;
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
