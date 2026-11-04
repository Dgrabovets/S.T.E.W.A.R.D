using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Steward.Homework.Models;

namespace Steward.Homework.Storage;

/// <summary>
/// Замечания владельца: предметные лежат в <c>remarks/&lt;имя предмета&gt;.json</c>, общие в <c>remarks/_all.json</c>.
/// Изменения идут под блокировкой (внутри процесса и между процессами Steward и Steward.Lms). Нечитаемый из-за временного сбоя файл не перезаписывается, повреждённый откладывается в <c>.bad</c>
/// </summary>
public sealed partial class RemarkStore
{
    /// <summary>
    /// Имя файла общих замечаний без расширения
    /// </summary>
    private const string ALL_FILE = "_all";

    /// <summary>
    /// Предельная длина имени файла предмета до метки хеша
    /// </summary>
    private const int MAX_NAME = 60;

    /// <summary>
    /// Сколько раз пробовать прочитать файл при временной ошибке ввода-вывода
    /// </summary>
    private const int READ_ATTEMPTS = 4;

    /// <summary>
    /// Пауза между попытками чтения, миллисекунды
    /// </summary>
    private const int RETRY_DELAY_MS = 50;

    /// <summary>
    /// Настройки JSON для файлов замечаний
    /// </summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>
    /// Имена, которые Windows не допускает для файлов
    /// </summary>
    private static readonly string[] Reserved = ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];

    /// <summary>
    /// Папка замечаний
    /// </summary>
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
    /// Результат чтения файла замечаний
    /// </summary>
    private enum ReadStateEnum
    {
        /// <summary>
        /// Файл прочитан или его ещё нет
        /// </summary>
        Ok,

        /// <summary>
        /// Файл есть, но не читается из-за ошибки ввода-вывода
        /// </summary>
        Unreadable,

        /// <summary>
        /// Файл прочитан, но это не корректный JSON замечаний
        /// </summary>
        Corrupt,
    }

    /// <summary>
    /// Добавляет замечание. Если такой же текст (без учёта регистра и лишних пробелов) уже есть в том же файле, повтор не добавляется
    /// </summary>
    /// <param name="remark">Замечание</param>
    /// <exception cref="IOException">Файл замечаний занят или недоступен: запись не выполнена, прежние замечания целы</exception>
    public void Add(Remark remark) => Locked(() => AddCore(remark));

    /// <summary>
    /// Предметные замечания (без общих). При сбое чтения пусто
    /// </summary>
    /// <param name="subject">Предмет</param>
    /// <returns>Замечания предмета</returns>
    public IReadOnlyList<Remark> Load(string subject) => ReadForView(SubjectFile(subject));

    /// <summary>
    /// Все замечания: общие и всех предметов. При сбое чтения файла он пропускается
    /// </summary>
    /// <returns>Все замечания</returns>
    public IReadOnlyList<Remark> LoadAll() => [.. Files().Order(StringComparer.Ordinal).SelectMany(ReadForView)];

    /// <summary>
    /// Заменяет замечание с тем же идентификатором. Если замечания нет, ничего не меняется. При переносе в другой файл сначала записывается новое место, потом удаляется старое
    /// </summary>
    /// <param name="remark">Новое содержимое замечания</param>
    /// <exception cref="IOException">Файл замечаний занят или недоступен: запись не выполнена, прежние замечания целы</exception>
    public void Update(Remark remark) => Locked(() =>
    {
        foreach (var path in Files())
        {
            var list = ReadForWrite(path);
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
                // Предмет или признак «общее» изменились: сначала целевой файл, потом убираем из старого
                var target = ReadForWrite(FileFor(remark));
                target.RemoveAll(existing => existing.Id == remark.Id);
                if (!target.Any(existing => Normalize(existing.Text) == Normalize(remark.Text)))
                    target.Add(remark);
                Write(FileFor(remark), target);
                list.RemoveAt(index);
                Write(path, list);
            }

            return;
        }
    });

    /// <summary>
    /// Удаляет замечание по идентификатору
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <exception cref="IOException">Файл замечаний занят или недоступен: запись не выполнена, прежние замечания целы</exception>
    public void Remove(string id) => Locked(() =>
    {
        foreach (var path in Files())
        {
            var list = ReadForWrite(path);
            if (list.RemoveAll(existing => existing.Id == id) > 0)
                Write(path, list);
        }
    });

    /// <summary>
    /// Выбирает замечания для подготовки: включённые, без повторов по тексту, общие и предметные поровну (нехватку одних добирают другими), новые по дате раньше
    /// </summary>
    /// <param name="subject">Предмет</param>
    /// <param name="kind">Нужный вид или <see langword="null"/> для любого</param>
    /// <param name="limit">Сколько замечаний взять не больше</param>
    /// <returns>Отобранные замечания</returns>
    public IReadOnlyList<Remark> ForPrepare(string subject, RemarkKindEnum? kind = null, int limit = 10)
    {
        if (limit <= 0)
            return [];

        /// <summary>
        /// Замечание включено и подходит по виду
        /// </summary>
        bool Fits(Remark remark) => remark.Enabled && (kind is null || remark.Kind == kind);

        var globalOnes = Distinct(Newest(ReadForView(AllFile()).Where(Fits)));
        var globalTexts = globalOnes.Select(remark => Normalize(remark.Text)).ToHashSet();

        // Предметное замечание с тем же текстом, что и общее, берём один раз: из общих
        var subjectOnes = Distinct(Newest(Load(subject).Where(Fits))).Where(remark => !globalTexts.Contains(Normalize(remark.Text))).ToList();

        var takeSubject = Math.Min(subjectOnes.Count, Math.Max(limit - limit / 2, limit - globalOnes.Count));
        var takeGlobal = Math.Min(globalOnes.Count, limit - takeSubject);

        return [.. subjectOnes.Take(takeSubject), .. globalOnes.Take(takeGlobal)];
    }

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
    /// Сводит текст замечания к одной строке: переносы и повторные пробелы заменяются одним пробелом. Так замечание не может «выпасть» из пункта списка в просьбе агенту
    /// </summary>
    /// <param name="text">Текст замечания</param>
    /// <returns>Одна строка без краевых пробелов</returns>
    public static string Flatten(string text) => Spaces().Replace(text, " ").Trim();

    /// <summary>
    /// Выполняет действие под блокировкой: общей для потоков процесса и именованным мьютексом для процессов
    /// </summary>
    /// <param name="action">Действие над файлами замечаний</param>
    /// <exception cref="IOException">Файлы замечаний заняты другим процессом</exception>
    private void Locked(Action action) => FileLock.Run(_directory, "Steward.Remarks.", action);

    /// <summary>
    /// Добавление замечания без блокировки: вызывается под ней
    /// </summary>
    /// <param name="remark">Замечание</param>
    private void AddCore(Remark remark)
    {
        var path = FileFor(remark);
        var list = ReadForWrite(path);
        if (list.Any(existing => Normalize(existing.Text) == Normalize(remark.Text)))
            return;

        list.Add(remark);
        Write(path, list);
    }

    /// <summary>
    /// Ставит новые замечания по дате раньше; при равной дате позднее добавленное раньше
    /// </summary>
    /// <param name="remarks">Замечания в порядке добавления</param>
    /// <returns>Замечания от новых к старым</returns>
    private static IEnumerable<Remark> Newest(IEnumerable<Remark> remarks) => remarks.Reverse().OrderByDescending(remark => remark.Date);

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
    /// Приводит текст к виду для сравнения: без регистра и лишних пробелов
    /// </summary>
    /// <param name="text">Текст</param>
    /// <returns>Нормализованный текст</returns>
    private static string Normalize(string text) => Flatten(text).ToLowerInvariant();

    /// <summary>
    /// Читает файл для показа и подготовки: при любом сбое пусто, ничего не меняется на диске
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    /// <returns>Замечания или пустой список</returns>
    private static List<Remark> ReadForView(string path) => TryRead(path, out var list) == ReadStateEnum.Ok ? list : [];

    /// <summary>
    /// Читает файл перед изменением. Нет файла: пусто. Повреждённый файл откладывается как <c>.bad</c> (прежний <c>.bad</c> заменяется). Нечитаемый из-за ошибки ввода-вывода после повторов: исключение, файл не трогается
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    /// <returns>Изменяемый список замечаний</returns>
    /// <exception cref="IOException">Файл не удалось прочитать</exception>
    private static List<Remark> ReadForWrite(string path)
    {
        switch (TryRead(path, out var list))
        {
            case ReadStateEnum.Ok:
                return list;
            case ReadStateEnum.Corrupt:
                File.Move(path, path + ".bad", overwrite: true);

                return [];
            default:
                throw new IOException("Файл замечаний недоступен для чтения: " + path);
        }
    }

    /// <summary>
    /// Читает файл замечаний, при ошибке ввода-вывода несколько раз с паузой
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    /// <param name="list">Прочитанные замечания, пусто если файла нет или чтение не удалось</param>
    /// <returns>Результат чтения</returns>
    private static ReadStateEnum TryRead(string path, out List<Remark> list)
    {
        list = [];
        for (var attempt = 1; attempt <= READ_ATTEMPTS; attempt++)
        {
            try
            {
                if (!File.Exists(path))
                    return ReadStateEnum.Ok;

                var text = File.ReadAllText(path);
                var parsed = JsonSerializer.Deserialize<List<Remark>>(text, Json);
                if (parsed is null)
                    return ReadStateEnum.Corrupt;

                list = [.. parsed.Where(remark => remark?.Text is not null && remark.Id is not null)];

                return ReadStateEnum.Ok;
            }
            catch (JsonException)
            {
                return ReadStateEnum.Corrupt;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt < READ_ATTEMPTS)
                    Thread.Sleep(RETRY_DELAY_MS);
            }
        }

        return ReadStateEnum.Unreadable;
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

    /// <summary>
    /// Любая последовательность пробельных знаков
    /// </summary>
    /// <returns>Регулярное выражение</returns>
    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
