using Steward.Schedule.Common.Models;
using Steward.Schedule.Scheduling.Interfaces;
using Steward.Schedule.Scheduling.Models;

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
    /// <param name="http">Клиент, используемый для запроса экспорта</param>
    /// <param name="options">Идентификатор таблицы и gid листа</param>
    public CsvScheduleReader(HttpClient http, SyncOptions options)
    {
        _http = http;
        _options = options;
    }

    /// <summary>
    /// Скачивает экспорт листа
    /// </summary>
    /// <param name="cancellationToken">Токен отмены загрузки</param>
    /// <returns>Текст листа в виде CSV. Значение объединённой ячейки повторено во всём диапазоне</returns>
    /// <exception cref="InvalidOperationException">
    /// Таблица не открыта по ссылке, либо Google вернул страницу ошибки
    /// </exception>
    public async Task<string> ReadCsvAsync(CancellationToken cancellationToken)
    {
        var body = await DownloadAsync($"&gid={Uri.EscapeDataString(_options.SheetGid)}", cancellationToken);

        return XlsxSheetParser.ToCsv(body);
    }

    /// <summary>
    /// Скачивает всю книгу и выбирает вкладки месяцев: прошлого, текущего и следующего, если они есть. Так занятия нового месяца подтягиваются, как только его вкладка появилась в таблице. Если в таблице нет вкладок с названиями месяцев, читается лист из <c>SHEET_GID</c>, как раньше
    /// </summary>
    /// <param name="today">Сегодняшняя дата в часовом поясе таблицы</param>
    /// <param name="cancellationToken">Токен отмены загрузки</param>
    /// <returns>Вкладки от старой к новой</returns>
    /// <exception cref="InvalidOperationException">
    /// Таблица не открыта по ссылке, либо Google вернул страницу ошибки
    /// </exception>
    public async Task<IReadOnlyList<ScheduleSheet>> ReadSheetsAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var workbook = await DownloadAsync(string.Empty, cancellationToken);
        var chosen = ScheduleTabs.Choose(XlsxSheetParser.ToCsvSheets(workbook), today);

        return chosen.Count > 0 ? chosen : [new ScheduleSheet(string.Empty, null, await ReadCsvAsync(cancellationToken))];
    }

    /// <summary>
    /// Скачивает экспорт таблицы в xlsx
    /// </summary>
    /// <param name="query">Добавка к адресу экспорта: пусто для всей книги или <c>&amp;gid=…</c> для одного листа</param>
    /// <param name="cancellationToken">Токен отмены загрузки</param>
    /// <returns>Байты файла xlsx</returns>
    /// <exception cref="InvalidOperationException">
    /// Таблица не открыта по ссылке, либо Google вернул страницу ошибки
    /// </exception>
    private async Task<byte[]> DownloadAsync(string query, CancellationToken cancellationToken)
    {
        var url = $"https://docs.google.com/spreadsheets/d/{Uri.EscapeDataString(_options.SpreadsheetId)}/export?format=xlsx{query}";

        using var response = await _http.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!response.IsSuccessStatusCode || body.Length < 2 || body[0] != (byte)'P' || body[1] != (byte)'K')
        {
            throw new InvalidOperationException(
                $"Google Sheets returned {(int)response.StatusCode}. Share the spreadsheet so anyone with the link can view it.");
        }

        return body;
    }
}
