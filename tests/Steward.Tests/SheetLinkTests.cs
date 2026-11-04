using Steward.Models;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Проверяет определение идентификатора таблицы и номера листа по ссылке
/// </summary>
public class SheetLinkTests
{
    /// <summary>
    /// Идентификатор берётся из полной ссылки на таблицу и из голого значения
    /// </summary>
    [Theory]
    [InlineData("https://docs.google.com/spreadsheets/d/1NKd10Q4Ld17T2c_g-71cqAHBAuPaVKNN5rPpWsL20ic/edit?gid=123#gid=123", "1NKd10Q4Ld17T2c_g-71cqAHBAuPaVKNN5rPpWsL20ic")]
    [InlineData("  1NKd10Q4Ld17T2c_g-71cqAHBAuPaVKNN5rPpWsL20ic ", "1NKd10Q4Ld17T2c_g-71cqAHBAuPaVKNN5rPpWsL20ic")]
    public void The_spreadsheet_id_is_found_in_a_link_or_given_bare(string text, string expected)
    {
        Assert.True(SheetLink.TryId(text, out var id));
        Assert.Equal(expected, id);
    }

    /// <summary>
    /// Чужой адрес и пустая строка идентификатора не дают
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("https://example.com/page")]
    [InlineData("привет")]
    public void Anything_else_gives_no_id(string text) => Assert.False(SheetLink.TryId(text, out _));

    /// <summary>
    /// Номер листа берётся из адреса вкладки или принимается числом
    /// </summary>
    [Theory]
    [InlineData("https://docs.google.com/spreadsheets/d/abc/edit?gid=987654#gid=987654", "987654")]
    [InlineData("https://docs.google.com/spreadsheets/d/abc/edit#gid=0", "0")]
    [InlineData("42", "42")]
    public void The_sheet_number_is_found_in_a_link_or_given_bare(string text, string expected)
    {
        Assert.True(SheetLink.TryGid(text, out var gid));
        Assert.Equal(expected, gid);
    }

    /// <summary>
    /// Ссылка без номера листа его не даёт
    /// </summary>
    [Fact]
    public void A_link_without_gid_gives_no_sheet_number() => Assert.False(SheetLink.TryGid("https://docs.google.com/spreadsheets/d/abc/edit", out _));

    /// <summary>
    /// Пояс из настроек всегда есть в списке, даже если системе он неизвестен, а сравнение с местным временем не падает на неизвестном поясе
    /// </summary>
    [Fact]
    public void The_current_zone_is_always_in_the_list_and_an_unknown_zone_has_no_comparison()
    {
        Assert.Contains(TimeZoneChoices.All("Nowhere/Land"), choice => choice.Id == "Nowhere/Land");
        Assert.Equal(string.Empty, TimeZoneChoices.Compare("Nowhere/Land", DateTimeOffset.UtcNow));
        Assert.NotEqual(string.Empty, TimeZoneChoices.Compare("Europe/Moscow", DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Кнопка определения вставляет найденное в поле и пишет итог, а при неудаче поле не трогает
    /// </summary>
    [Fact]
    public void Detecting_fills_the_field_and_a_miss_leaves_it_alone()
    {
        var model = new SettingsViewModel
        {
            SpreadsheetId = "old",
            SpreadsheetLink = "https://docs.google.com/spreadsheets/d/NEWID1234567890/edit",
        };

        model.DetectSpreadsheetCommand.Execute(null);
        Assert.Equal("NEWID1234567890", model.SpreadsheetId);

        model.SpreadsheetLink = "ерунда";
        model.DetectSpreadsheetCommand.Execute(null);
        Assert.Equal("NEWID1234567890", model.SpreadsheetId);
        Assert.Contains("нет идентификатора", model.SpreadsheetNote);
    }
}
