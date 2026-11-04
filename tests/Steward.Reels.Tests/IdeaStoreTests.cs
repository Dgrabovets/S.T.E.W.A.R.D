using Steward.Ideas.Models;
using Steward.Ideas.Storage;
using Steward.Reels.Tests.Fakes;

namespace Steward.Reels.Tests;

/// <summary>
/// Проверяет папку идей: хранение, порядок, решения и защиту от чужих имён файлов
/// </summary>
public class IdeaStoreTests
{
    /// <summary>
    /// Сохранённая идея читается обратно, свежие идут первыми
    /// </summary>
    [Fact]
    public void Saved_ideas_load_newest_first()
    {
        using var directory = new TempDirectory();
        var store = new IdeaStore(directory.Path);

        store.Save(Make("r1-1", 8));
        store.Save(Make("r2-1", 10));

        Assert.Equal(["r2-1", "r1-1"], store.LoadAll().Select(idea => idea.Id).ToArray());
        Assert.True(store.Exists("r1-1"));
    }

    /// <summary>
    /// Решение записывается в файл, а для неизвестной идеи ничего не меняется
    /// </summary>
    [Fact]
    public void SetState_updates_a_known_idea_only()
    {
        using var directory = new TempDirectory();
        var store = new IdeaStore(directory.Path);
        store.Save(Make("r1-1", 8));

        Assert.True(store.SetState("r1-1", IdeaStateEnum.Approved));
        Assert.False(store.SetState("r9-9", IdeaStateEnum.Approved));
        Assert.Equal(IdeaStateEnum.Approved, store.LoadAll().Single().State);
    }

    /// <summary>
    /// Идентификатор с путём или лишними символами не принимается
    /// </summary>
    [Theory]
    [InlineData("..\\evil")]
    [InlineData("a/b")]
    [InlineData("")]
    [InlineData(null)]
    public void Unsafe_ids_are_rejected(string? id) => Assert.False(IdeaStore.IsValidId(id));

    /// <summary>
    /// Испорченный файл идеи пропускается, остальные читаются
    /// </summary>
    [Fact]
    public void A_broken_file_is_skipped()
    {
        using var directory = new TempDirectory();
        var store = new IdeaStore(directory.Path);
        store.Save(Make("r1-1", 8));
        File.WriteAllText(Path.Combine(directory.Path, "bad.json"), "{ не json");

        Assert.Single(store.LoadAll());
    }

    /// <summary>
    /// Собирает идею, созданную в указанный час
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="hour">Час 25 октября 2026</param>
    /// <returns>Идея</returns>
    private static Idea Make(string id, int hour) => new()
    {
        Id = id,
        CreatedAt = new DateTimeOffset(2026, 10, 25, hour, 0, 0, TimeSpan.Zero),
        SourceUrl = "https://www.instagram.com/reel/x/",
        Title = id,
    };
}