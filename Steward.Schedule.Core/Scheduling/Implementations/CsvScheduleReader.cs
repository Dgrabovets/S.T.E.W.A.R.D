using Steward.Schedule.Common.Models;
using Steward.Schedule.Scheduling.Interfaces;

namespace Steward.Schedule.Scheduling.Implementations;

/// <summary>
/// Скачивает публичный лист Google Sheets и раскрывает объединённые ячейки
/// </summary>
public sealed class CsvScheduleReader : IScheduleSource
{
    /// <summary>
    /// Клиент, используемый для запроса экспорта
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// Идентификатор таблицы и gid листа
    /// </summary>
    private readonly SyncOptions _options;

    /// <summary>
    /// Создаёт читатель таблицы из <paramref name="options"/>
    /// </summary>
    /// <param name="http">
    /// Клиент, используемый для запроса экспорта
    /// </param>
    /// <param name="options">
    /// Идентификатор таблицы и gid листа
    /// </param>
    public CsvScheduleReader(HttpClient http, SyncOptions options)
    {
        _http = http;
        _options = options;
    }

    /// <summary>
    /// Скачивает экспорт листа
    /// </summary>
    /// <param name="cancellationToken">
    /// Токен отмены загрузки
    /// </param>
    /// <returns>
    /// Текст листа в виде CSV. Значение объединённой ячейки повторено во всём диапазоне
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Таблица не открыта по ссылке, либо Google вернул страницу ошибки
    /// </exception>
    public async Task<string> ReadCsvAsync(CancellationToken cancellationToken)
    {
        var url = $"https://docs.google.com/spreadsheets/d/{Uri.EscapeDataString(_options.SpreadsheetId)}/export?format=xlsx&gid={Uri.EscapeDataString(_options.SheetGid)}";

        using var response = await _http.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!response.IsSuccessStatusCode || body.Length < 2 || body[0] != (byte)'P' || body[1] != (byte)'K')
        {
            throw new InvalidOperationException(
                $"Google Sheets returned {(int)response.StatusCode}. Share the spreadsheet so anyone with the link can view it.");
        }

        return XlsxSheetParser.ToCsv(body);
    }
}
