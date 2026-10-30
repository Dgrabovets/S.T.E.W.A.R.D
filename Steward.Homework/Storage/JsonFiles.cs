using System.Text.Json;

namespace Steward.Homework.Storage;

/// <summary>
/// Чтение JSON-файла владельца перед изменением. Нечитаемый из-за временного сбоя файл не перезаписывается пустым, повреждённый откладывается в <c>&lt;имя&gt;.bad</c>
/// </summary>
internal static class JsonFiles
{
    /// <summary>
    /// Сколько раз пробовать прочитать файл при ошибке ввода-вывода
    /// </summary>
    private const int READ_ATTEMPTS = 4;

    /// <summary>
    /// Пауза между попытками чтения, миллисекунды
    /// </summary>
    private const int RETRY_DELAY_MS = 50;

    /// <summary>
    /// Читает файл перед изменением. Нет файла: <see langword="null"/>. Повреждённый файл переносится в <c>.bad</c> (прежний <c>.bad</c> заменяется) и тоже даёт <see langword="null"/>, чтобы записать новый. Файл, который не читается после нескольких попыток, не трогается: выбрасывается исключение и запись не выполняется
    /// </summary>
    /// <typeparam name="T">Тип содержимого</typeparam>
    /// <param name="path">Файл</param>
    /// <param name="options">Настройки JSON</param>
    /// <returns>Содержимое или <see langword="null"/>, если файла нет или он был испорчен</returns>
    /// <exception cref="IOException">Файл не удалось прочитать: прежнее содержимое цело</exception>
    public static T? ReadForWrite<T>(string path, JsonSerializerOptions options) where T : class
    {
        for (var attempt = 1; attempt <= READ_ATTEMPTS; attempt++)
        {
            string text;
            try
            {
                if (!File.Exists(path))
                    return null;

                text = File.ReadAllText(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt < READ_ATTEMPTS)
                    Thread.Sleep(RETRY_DELAY_MS);

                continue;
            }

            try
            {
                if (JsonSerializer.Deserialize<T>(text, options) is { } value)
                    return value;
            }
            catch (JsonException)
            {
                // Испорченный файл откладывается ниже
            }

            File.Move(path, path + ".bad", overwrite: true);

            return null;
        }

        throw new IOException("Файл недоступен для чтения, изменение не записано: " + path);
    }
}
