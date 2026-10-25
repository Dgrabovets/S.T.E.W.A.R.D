using System.Text.Encodings.Web;
using System.Text.Json;

namespace Steward.Schedule.Common.Helpers;

/// <summary>
/// Собирает заметку Singularity в виде дельты Quill: текст, горизонтальная линия и гиперссылка с видимой подписью. Так заметку хранит и показывает приложение
/// </summary>
public sealed class QuillDelta
{
    /// <summary>
    /// Сериализатор дельты. Кириллица остаётся символами, а не кодами <c>\u</c>
    /// </summary>
    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Операции в порядке записи
    /// </summary>
    private readonly List<object> _ops = [];

    /// <summary>
    /// Добавляет текст как есть. Перевод строки в конце нужно поставить самому
    /// </summary>
    /// <param name="text">Текст заметки</param>
    /// <returns>Эта же дельта для цепочки вызовов</returns>
    public QuillDelta Text(string text)
    {
        _ops.Add(new { insert = text });

        return this;
    }

    /// <summary>
    /// Добавляет горизонтальную линию, которой отделяют текст от ссылок
    /// </summary>
    /// <returns>Эта же дельта для цепочки вызовов</returns>
    public QuillDelta Divider()
    {
        _ops.Add(new { insert = new { divider = true } });

        return this;
    }

    /// <summary>
    /// Добавляет гиперссылку с видимой подписью и переводом строки после неё
    /// </summary>
    /// <param name="label">Видимый текст ссылки, например «Письмо»</param>
    /// <param name="url">Адрес, на который ведёт ссылка</param>
    /// <returns>Эта же дельта для цепочки вызовов</returns>
    public QuillDelta Link(string label, string url)
    {
        _ops.Add(new { insert = label, attributes = new { link = url } });
        _ops.Add(new { insert = "\n" });

        return this;
    }

    /// <summary>
    /// Записывает дельту в JSON-массив операций, который принимает поле <c>note</c> API
    /// </summary>
    /// <returns>Строка JSON</returns>
    public override string ToString() => JsonSerializer.Serialize(_ops, Json);
}