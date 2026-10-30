using System.Text.Json;
using Steward.Lms.Common;

namespace Steward.Lms.Tests;

/// <summary>
/// Настройка лимита автоподготовки работ в <c>lmssettings.json</c>
/// </summary>
public class PrepareSettingsTests
{
    /// <summary>
    /// Старый файл настроек без нового поля даёт лимит 2, а заданное значение читается
    /// </summary>
    [Fact]
    public void MaxPreparePerRun_defaults_to_two_and_is_read_from_the_file()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var old = JsonSerializer.Deserialize<LmsSettings>("""{"login": "x", "maxFetchPerRun": 6, "createTasks": false}""", options)!;
        var set = JsonSerializer.Deserialize<LmsSettings>("""{"maxPreparePerRun": 5}""", options)!;

        Assert.Equal(2, old.MaxPreparePerRun);
        Assert.Equal(5, set.MaxPreparePerRun);
        Assert.Equal(20, old.PrepareBudgetMinutes);
    }

    /// <summary>
    /// Старый файл настроек без срока автоподготовки даёт 21 день, а заданное значение читается
    /// </summary>
    [Fact]
    public void PrepareMaxAgeDays_defaults_to_21_and_is_read_from_the_file()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var old = JsonSerializer.Deserialize<LmsSettings>("""{"login": "x", "maxPreparePerRun": 3}""", options)!;
        var set = JsonSerializer.Deserialize<LmsSettings>("""{"prepareMaxAgeDays": 7}""", options)!;

        Assert.Equal(21, old.PrepareMaxAgeDays);
        Assert.Equal(7, set.PrepareMaxAgeDays);
    }
}
