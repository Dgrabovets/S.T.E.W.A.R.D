using Steward.Mail.Services;

namespace Steward.Mail.Tests;

/// <summary>
/// Проверяет список обработанных писем в файле
/// </summary>
public class JsonStateStoreTests : IDisposable
{
    /// <summary>
    /// Папка теста, которую удаляет Dispose
    /// </summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "steward-mail-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Создаёт папку теста
    /// </summary>
    public JsonStateStoreTests() => Directory.CreateDirectory(_directory);

    /// <summary>
    /// Удаляет папку теста
    /// </summary>
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    /// <summary>
    /// Без файла список пуст
    /// </summary>
    [Fact]
    public void A_missing_file_gives_an_empty_list()
    {
        var store = new JsonStateStore(Path.Combine(_directory, "processed.json"));

        Assert.False(store.IsProcessed("a"));
    }

    /// <summary>
    /// Сохранённые письма находятся после перезагрузки из файла, повторная отметка ничего не ломает
    /// </summary>
    [Fact]
    public void Saved_ids_survive_a_reload()
    {
        var path = Path.Combine(_directory, "processed.json");
        var store = new JsonStateStore(path);
        store.MarkProcessed("a");
        store.MarkProcessed("a");
        store.Save();

        var reloaded = new JsonStateStore(path);

        Assert.True(reloaded.IsProcessed("a"));
        Assert.False(reloaded.IsProcessed("b"));
    }

    /// <summary>
    /// Не сохранённая отметка не попадает в файл
    /// </summary>
    [Fact]
    public void An_unsaved_mark_is_not_written()
    {
        var path = Path.Combine(_directory, "processed.json");
        new JsonStateStore(path).MarkProcessed("a");

        Assert.False(new JsonStateStore(path).IsProcessed("a"));
    }

    /// <summary>
    /// Файл хранит только последние две тысячи писем, а запись идёт через временный файл, который не остаётся
    /// </summary>
    [Fact]
    public void Save_keeps_the_latest_entries_and_leaves_no_temporary_file()
    {
        var path = Path.Combine(_directory, "processed.json");
        var store = new JsonStateStore(path);
        for (var number = 0; number < 2005; number++)
            store.MarkProcessed($"id-{number}");

        store.Save();

        var reloaded = new JsonStateStore(path);
        Assert.False(reloaded.IsProcessed("id-4"));
        Assert.True(reloaded.IsProcessed("id-5"));
        Assert.True(reloaded.IsProcessed("id-2004"));
        Assert.Equal(["processed.json"], Directory.GetFiles(_directory).Select(file => Path.GetFileName(file)).ToArray());
    }
}