using System.Text.Json;
using System.Text.Json.Serialization;

namespace Steward.Status;

/// <summary>
/// Состояние процесса в файле статуса. Незнакомое значение читается как <see cref="Unknown"/>, а не ломает чтение
/// </summary>
[JsonConverter(typeof(ProcessStateConverter))]
public enum ProcessState
{
    /// <summary>
    /// Значение не распознано: файл написан более новой версией формата
    /// </summary>
    Unknown,

    /// <summary>
    /// Процесс ничего не делает и ждёт своего времени
    /// </summary>
    Idle,

    /// <summary>
    /// Процесс выполняется прямо сейчас
    /// </summary>
    Running,

    /// <summary>
    /// Последний запуск закончился успешно
    /// </summary>
    Ok,

    /// <summary>
    /// Что-то готово и ждёт решения пользователя
    /// </summary>
    NeedsAttention,

    /// <summary>
    /// Последний запуск закончился ошибкой
    /// </summary>
    Failed,
}

/// <summary>
/// Пишет состояние строкой в camelCase и читает незнакомую строку как <see cref="ProcessState.Unknown"/>
/// </summary>
public sealed class ProcessStateConverter : JsonConverter<ProcessState>
{
    /// <summary>
    /// Читает состояние из строки
    /// </summary>
    /// <param name="reader">
    /// Читатель JSON
    /// </param>
    /// <param name="typeToConvert">
    /// Тип состояния
    /// </param>
    /// <param name="options">
    /// Настройки сериализатора
    /// </param>
    /// <returns>
    /// Состояние или <see cref="ProcessState.Unknown"/>
    /// </returns>
    public override ProcessState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            reader.Skip();
            return ProcessState.Unknown;
        }

        return Enum.TryParse<ProcessState>(reader.GetString(), ignoreCase: true, out var state) ? state : ProcessState.Unknown;
    }

    /// <summary>
    /// Пишет состояние строкой с маленькой первой буквой
    /// </summary>
    /// <param name="writer">
    /// Писатель JSON
    /// </param>
    /// <param name="value">
    /// Состояние
    /// </param>
    /// <param name="options">
    /// Настройки сериализатора
    /// </param>
    public override void Write(Utf8JsonWriter writer, ProcessState value, JsonSerializerOptions options)
    {
        var name = value.ToString();
        writer.WriteStringValue(char.ToLowerInvariant(name[0]) + name[1..]);
    }
}
