using Steward.Status.Running;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Проверяет, что карточка журнала не повторяет над плашками то, что уже есть в плашках
/// </summary>
public class RunRowViewModelTests
{
    /// <summary>
    /// Запуск с ошибкой и выводом: время начала, конец и текст
    /// </summary>
    /// <param name="detail">Ошибка из статуса</param>
    /// <param name="summary">Сводка запуска</param>
    /// <param name="output">Вывод программы</param>
    /// <returns>Строка журнала</returns>
    private static RunRowViewModel Row(string? detail, string? summary, string? output)
    {
        var start = new DateTimeOffset(2026, 10, 25, 18, 9, 29, TimeSpan.FromHours(1));

        return new RunRowViewModel(new RunRecord("mail", "Почта", "manual", start, start.AddSeconds(4), 1, RunOutcomeEnum.Failed, detail, summary, output), _ => { });
    }

    /// <summary>
    /// Ошибка, которая уже есть в выводе (с типом исключения или без), над плашками не повторяется
    /// </summary>
    [Fact]
    public void An_error_that_is_already_in_the_output_is_not_repeated_above_it()
    {
        var row = Row(
            "HttpRequestException: SingularityApp ответил 400: Invalid date value",
            null,
            "25.10.2026 18:09:34  Ошибка: System.Net.Http.HttpRequestException: SingularityApp ответил 400: Invalid date value\nat Program.Main()");

        Assert.False(row.HasDetail);
        Assert.True(row.HasOutput);
    }

    /// <summary>
    /// Ошибка, которой нет в выводе, остаётся над плашками, так же как сводка, которой там нет
    /// </summary>
    [Fact]
    public void An_error_and_a_summary_missing_from_the_output_stay_visible()
    {
        var row = Row("Код завершения 1: нет доступа к LMS", "Запущено 3 письма", "25.10.2026 18:09:34  Инфо: что-то другое");

        Assert.True(row.HasDetail);
        Assert.True(row.HasSummary);
    }

    /// <summary>
    /// Сводка, которая повторяет строку вывода, над плашками не показывается
    /// </summary>
    [Fact]
    public void A_summary_that_repeats_an_output_line_is_hidden() =>
        Assert.False(Row(null, "Прочее: 1", "25.10.2026 18:18:44  Прочее: 1").HasSummary);

    /// <summary>
    /// Без вывода ошибка и сводка показываются как есть
    /// </summary>
    [Fact]
    public void Without_output_the_error_and_the_summary_are_shown()
    {
        var row = Row("Код завершения 2", "Завершено без ошибок", null);

        Assert.True(row.HasDetail);
        Assert.True(row.HasSummary);
        Assert.False(row.HasOutput);
    }
}