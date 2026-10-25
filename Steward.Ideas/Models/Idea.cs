using System.Text.Json.Serialization;

namespace Steward.Ideas.Models;

/// <summary>
/// Идея из сохранённого рилса: откуда она, что в ней сказано и что по ней решено
/// </summary>
public sealed record Idea
{
    /// <summary>
    /// Версия формата файла: пригодится, когда у идеи появятся новые поля
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Идентификатор: он же имя файла. Латиница в нижнем регистре, цифры, дефис и подчёркивание
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Когда идея получена
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Адрес рилса
    /// </summary>
    public required string SourceUrl { get; init; }

    /// <summary>
    /// Короткое название для списка: первая строка подписи или начало расшифровки
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Автор рилса или пустая строка, если он неизвестен
    /// </summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>
    /// Подпись под рилсом
    /// </summary>
    public string Caption { get; init; } = string.Empty;

    /// <summary>
    /// Расшифровка речи из ролика
    /// </summary>
    public string Transcript { get; init; } = string.Empty;

    /// <summary>
    /// Что решено с идеей
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public IdeaStateEnum State { get; init; } = IdeaStateEnum.New;
}