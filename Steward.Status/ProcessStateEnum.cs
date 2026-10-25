using System.Text.Json;
using System.Text.Json.Serialization;

namespace Steward.Status;

/// <summary>
/// Состояние процесса в файле статуса. Незнакомое значение читается как <see cref="Unknown"/>, а не ломает чтение
/// </summary>
[JsonConverter(typeof(ProcessStateConverter))]
public enum ProcessStateEnum
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
/// Пишет состояние строкой в camelCase и читает незнакомую строку как <see cref="ProcessStateEnum.Unknown"/>
/// </summary>
public sealed class ProcessStateConverter : JsonConverter<ProcessStateEnum>
{
    /// <summary>
    /// Читает состояние из строки
    /// </summary>
    /// <param name="reader">Читатель JSON</param>
    /// <param name="typeToConvert">Тип состояния</param>
    /// <param name="options">Настройки сериализатора</param>
    /// <returns>
    /// Состояние или <see cref="ProcessStateEnum.Unknown"/>
    /// </returns>
    public override ProcessStateEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            reader.Skip();
            return ProcessStateEnum.Unknown;
        }

        return Enum.TryParse<ProcessStateEnum>(reader.GetString(), ignoreCase: true, out var state) ? state : ProcessStateEnum.Unknown;
    }

    /// <summary>
    /// Пишет состояние строкой с маленькой первой буквой
    /// </summary>
    /// <param name="writer">Писатель JSON</param>
    /// <param name="value">Состояние</param>
    /// <param name="options">Настройки сериализатора</param>
    public override void Write(Utf8JsonWriter writer, ProcessStateEnum value, JsonSerializerOptions options)
    {
        var name = value.ToString();
        writer.WriteStringValue(char.ToLowerInvariant(name[0]) + name[1..]);
    }
}
