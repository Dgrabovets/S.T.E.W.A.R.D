using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Steward.Monitoring.Models;
using Steward.Status.Common;

namespace Steward.Monitoring.Implementations;

/// <summary>
/// Копит историю сверок расписания. Docker отдаёт только хвост журнала контейнера, поэтому без копилки в окне виделась бы одна последняя сверка
/// </summary>
public static class ScheduleHistory
{
    /// <summary>
    /// Сколько записей хранить
    /// </summary>
    public const int LIMIT = 300;

    /// <summary>
    /// Настройки записи: тон хранится словом
    /// </summary>
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    /// <summary>
    /// Файл истории в общей папке данных
    /// </summary>
    private static string FilePath() => Path.Combine(DataLocation.DataDirectory(), "schedule", "history.json");

    /// <summary>
    /// Файл с моментом очистки журнала: записи не позже него не возвращаются из хвоста журнала контейнера
    /// </summary>
    private static string ClearedFilePath() => Path.Combine(DataLocation.DataDirectory(), "schedule", "cleared.txt");

    /// <summary>
    /// Файл с моментами записей, которые человек удалил по одной: из хвоста журнала контейнера они не возвращаются
    /// </summary>
    private static string RemovedFilePath() => Path.Combine(DataLocation.DataDirectory(), "schedule", "removed.txt");

    /// <summary>
    /// Удаляет одну запись из истории и запоминает её момент, чтобы она не вернулась из хвоста журнала контейнера
    /// </summary>
    /// <param name="entry">Запись, которую убирают</param>
    public static void Remove(LogEntry entry)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RemovedFilePath())!);
            File.AppendAllText(RemovedFilePath(), entry.Time.UtcTicks + Environment.NewLine);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // ignore
        }

        Save([.. Load().Where(stored => stored.Time != entry.Time)]);
    }

    /// <summary>
    /// Читает моменты удалённых записей
    /// </summary>
    /// <returns>Набор моментов в тиках UTC</returns>
    private static HashSet<long> RemovedTicks()
    {
        try
        {
            return File.Exists(RemovedFilePath())
                ? [.. File.ReadLines(RemovedFilePath()).Select(line => long.TryParse(line, out var ticks) ? ticks : 0).Where(ticks => ticks > 0)]
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Добавляет свежие записи к сохранённой истории, записывает результат и отдаёт его
    /// </summary>
    /// <param name="fresh">Записи из хвоста журнала контейнера от новых к старым</param>
    /// <returns>Вся история от новых к старым без записей, стёртых очисткой</returns>
    public static IReadOnlyList<LogEntry> Update(IReadOnlyList<LogEntry> fresh)
    {
        var merged = Merge(Load(), fresh);
        var cleared = ClearedAt();
        var removed = RemovedTicks();
        var kept = merged.Where(entry => (cleared is not { } stamp || entry.Time > stamp) && !removed.Contains(entry.Time.UtcTicks)).ToArray();
        if (fresh.Count > 0)
            Save(kept);

        return kept;
    }

    /// <summary>
    /// Стирает историю: сохранённые записи удаляются, а те, что ещё лежат в хвосте журнала контейнера, больше не показываются
    /// </summary>
    public static void Clear()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ClearedFilePath())!);
            File.WriteAllText(ClearedFilePath(), DateTimeOffset.Now.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // ignore
        }

        Save([]);
    }

    /// <summary>
    /// Читает момент последней очистки
    /// </summary>
    /// <returns>Момент или <see langword="null"/>, если журнал не очищали</returns>
    private static DateTimeOffset? ClearedAt()
    {
        try
        {
            var path = ClearedFilePath();
            if (File.Exists(path) && DateTimeOffset.TryParse(File.ReadAllText(path).Trim(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var stamp))
                return stamp;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // ignore
        }

        return null;
    }

    /// <summary>
    /// Читает сохранённую историю
    /// </summary>
    /// <returns>Записи от новых к старым. Пустой список, если файла нет или он испорчен</returns>
    public static IReadOnlyList<LogEntry> Load()
    {
        try
        {
            var path = FilePath();
            if (!File.Exists(path))
                return [];

            return JsonSerializer.Deserialize<List<LogEntry>>(File.ReadAllText(path), Options) ?? [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Записывает историю. Сбой записи не мешает работе: история тогда пополнится при следующем опросе
    /// </summary>
    /// <param name="entries">Записи от новых к старым</param>
    public static void Save(IReadOnlyList<LogEntry> entries)
    {
        try
        {
            var path = FilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(entries, Options));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // ignore
        }
    }

    /// <summary>
    /// Дописывает свежие записи к сохранённым. Всё, что старше самой старой свежей записи, берётся из сохранённого: хвост журнала мог обрезать начало её прогона. Повторные тихие сверки склеиваются в одну запись
    /// </summary>
    /// <param name="stored">Сохранённая история от новых к старым</param>
    /// <param name="fresh">Записи из хвоста журнала от новых к старым</param>
    /// <returns>Общая история от новых к старым, не длиннее <see cref="LIMIT"/></returns>
    public static IReadOnlyList<LogEntry> Merge(IReadOnlyList<LogEntry> stored, IReadOnlyList<LogEntry> fresh)
    {
        if (fresh.Count == 0)
            return stored;

        var oldest = fresh[^1];
        var older = stored.Where(entry => entry.Time <= oldest.Time).ToList();
        var shown = older.Count > 0 && older[0].Time == oldest.Time ? fresh.Take(fresh.Count - 1) : fresh;
        var merged = shown.Concat(older).ToList();

        var join = merged.Count - older.Count;
        if (older.Count > 0 && join > 0 && join < merged.Count && Same(merged[join - 1], merged[join]))
        {
            var newer = merged[join - 1];
            var previous = merged[join];
            merged[join - 1] = newer with { Repeat = Math.Max(newer.Repeat, previous.Repeat) };
            merged.RemoveAt(join);
        }

        return merged.Count > LIMIT ? merged.Take(LIMIT).ToArray() : merged;
    }

    /// <summary>
    /// Проверяет, что две соседние записи — одна и та же тихая сверка, разрезанная границей хвоста журнала
    /// </summary>
    /// <param name="newer">Более новая запись</param>
    /// <param name="older">Более старая запись</param>
    /// <returns>
    /// <see langword="true"/>, если их нужно склеить
    /// </returns>
    private static bool Same(LogEntry newer, LogEntry older) =>
        newer.Tone == LogToneEnum.Quiet && older.Tone == LogToneEnum.Quiet && newer.Detail == older.Detail && newer.Title == older.Title;
}
