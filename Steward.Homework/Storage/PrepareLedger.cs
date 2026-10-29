using System.Text.Encodings.Web;
using System.Text.Json;

namespace Steward.Homework.Storage;

/// <summary>
/// Учёт автоподготовки: попытки подряд после ошибки агента (<c>prepare-attempts.json</c>) и задания, чья сданная работа уже стала образцом (<c>sample-registered.json</c>).
/// Каждое изменение идёт под общей блокировкой процессов и пишется атомарно через временный файл с уникальным именем. Нет файла или он испорчен: учёт пуст
/// </summary>
/// <param name="directory">Папка домашних заданий</param>
internal sealed class PrepareLedger(string directory)
{
    /// <summary>
    /// Начало имени мьютекса блокировки учёта
    /// </summary>
    private const string LOCK_PREFIX = "Steward.Prepare.";

    /// <summary>
    /// Читает и пишет JSON с отступами, кириллица остаётся буквами
    /// </summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Файл попыток: ключ «занятие:номер» и число неудач подряд
    /// </summary>
    private string AttemptsFile => Path.Combine(directory, "prepare-attempts.json");

    /// <summary>
    /// Файл заданий, работа которых уже зарегистрирована образцом
    /// </summary>
    private string SamplesFile => Path.Combine(directory, "sample-registered.json");

    /// <summary>
    /// Сколько неудачных попыток подряд было у задания
    /// </summary>
    /// <param name="key">Ключ «занятие:номер»</param>
    /// <returns>Число попыток, 0 если не было</returns>
    public int Attempts(string key) => Read<Dictionary<string, int>>(AttemptsFile)?.GetValueOrDefault(key) ?? 0;

    /// <summary>
    /// Все попытки одним чтением файла: для карточек всех заданий занятия
    /// </summary>
    /// <returns>Число неудач подряд по ключу «занятие:номер»</returns>
    public IReadOnlyDictionary<string, int> AllAttempts() => Read<Dictionary<string, int>>(AttemptsFile) ?? [];

    /// <summary>
    /// Записывает число попыток задания; 0 убирает запись
    /// </summary>
    /// <param name="key">Ключ «занятие:номер»</param>
    /// <param name="count">Число попыток</param>
    /// <exception cref="IOException">Файл занят другим процессом или недоступен</exception>
    public void SetAttempts(string key, int count) => FileLock.Run(directory, LOCK_PREFIX, () =>
    {
        var attempts = new Dictionary<string, int>(Read<Dictionary<string, int>>(AttemptsFile) ?? [], StringComparer.Ordinal);
        if (count > 0)
            attempts[key] = count;
        else if (!attempts.Remove(key))
            return;

        Write(AttemptsFile, attempts);
    });

    /// <summary>
    /// Работа задания уже стала образцом
    /// </summary>
    /// <param name="key">Ключ «занятие:номер»</param>
    /// <returns>
    /// <see langword="true"/>, если образец уже регистрировался
    /// </returns>
    public bool SampleRegistered(string key) => Read<List<string>>(SamplesFile)?.Contains(key, StringComparer.Ordinal) ?? false;

    /// <summary>
    /// Запоминает, что работа задания стала образцом: повторно она не регистрируется, даже если владелец удалит образец
    /// </summary>
    /// <param name="key">Ключ «занятие:номер»</param>
    /// <exception cref="IOException">Файл занят другим процессом или недоступен</exception>
    public void MarkSample(string key) => FileLock.Run(directory, LOCK_PREFIX, () =>
    {
        var keys = new SortedSet<string>(Read<List<string>>(SamplesFile) ?? [], StringComparer.Ordinal);
        if (keys.Add(key))
            Write(SamplesFile, keys);
    });

    /// <summary>
    /// Читает JSON-файл
    /// </summary>
    /// <typeparam name="T">Тип содержимого</typeparam>
    /// <param name="path">Файл</param>
    /// <returns>Содержимое или <see langword="null"/>, если файла нет, он испорчен или не читается</returns>
    private static T? Read<T>(string path) where T : class
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) : null;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Записывает файл через временный с уникальным именем и переносом на место
    /// </summary>
    /// <param name="path">Файл</param>
    /// <param name="value">Содержимое</param>
    private void Write(string path, object value)
    {
        Directory.CreateDirectory(directory);

        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(value, Json));
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            File.Delete(temp);

            throw;
        }
    }
}
