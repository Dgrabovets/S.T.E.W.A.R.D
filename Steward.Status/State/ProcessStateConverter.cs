using System.Text.Json.Serialization;
using System.Text.Json;

namespace Steward.Status.State;

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
