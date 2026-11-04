using Steward.Homework.Models;
using Steward.Homework.Storage;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Общие указания и образцы по предметам: исключения предмета, отслеживание изменений оригинала и окно «Уточнить»
/// </summary>
public sealed class GuideScopeTests : IDisposable
{
    /// <summary>
    /// Папка теста
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "steward-guides-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>
    /// Хранилище материалов в папке теста
    /// </summary>
    private readonly GuideStore _guides;

    /// <summary>
    /// Создаёт папку теста
    /// </summary>
    public GuideScopeTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        _guides = new GuideStore(_root);
    }

    /// <summary>
    /// Удаляет папку теста
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Записывает файл-оригинал
    /// </summary>
    /// <param name="name">Имя файла</param>
    /// <param name="text">Содержимое</param>
    /// <returns>Полный путь</returns>
    private string Original(string name, string text)
    {
        var path = Path.Combine(_root, "src", name);
        File.WriteAllText(path, text);

        return path;
    }

    /// <summary>
    /// Исключённые для предмета материалы при подготовке не берутся, у других предметов они остаются, а явный выбор владельца сильнее исключения
    /// </summary>
    [Fact]
    public void Excluded_materials_are_skipped_for_that_subject_only_and_an_explicit_choice_wins()
    {
        var concept = _guides.AddFile("Общие", GuideKindEnum.Guide, Original("концепция.md", "Концепция игры"));
        var format = _guides.AddFile("Общие", GuideKindEnum.Guide, Original("оформление.md", "Оформление"));
        var excluded = new HashSet<string>(StringComparer.Ordinal) { concept.Id };

        var forLanguage = _guides.Materials("Иностранный язык", null, excluded);
        var forDesign = _guides.Materials("Гейм-дизайн");

        Assert.Single(forLanguage.Guides);
        Assert.EndsWith("оформление.md", forLanguage.Guides[0], StringComparison.Ordinal);
        Assert.Equal(2, forDesign.Guides.Count);

        var explicitChoice = _guides.Materials("Иностранный язык", new HashSet<string>(StringComparer.Ordinal) { concept.Id, format.Id }, excluded);

        Assert.Equal(2, explicitChoice.Guides.Count);
    }

    /// <summary>
    /// Исключения хранятся у предмета, не задевают соседей и читаются старой записью без этого поля
    /// </summary>
    [Fact]
    public void Excluded_guides_are_stored_per_subject_and_old_records_still_load()
    {
        var store = new HomeworkStore(Path.Combine(_root, "hw"));

        store.SetSubjectExcludedGuides("Иностранный язык", ["b", "a", "a"]);

        Assert.Equal(["a", "b"], store.SubjectOptionsFor("Иностранный язык").Excluded.ToArray());
        Assert.Empty(store.SubjectOptionsFor("Гейм-дизайн").Excluded);

        File.WriteAllText(Path.Combine(_root, "hw", "subject-options.json"), """{"Философия":{"autoPrepare":true,"taskSource":1}}""");

        Assert.Equal(new SubjectOptions(true), store.SubjectOptionsFor("Философия"));
        Assert.Empty(store.SubjectOptionsFor("Философия").Excluded);
    }

    /// <summary>
    /// Копия обновляется, когда оригинал изменился: другой размер или время, а если оригинал не менялся или исчез, копия остаётся как была
    /// </summary>
    [Fact]
    public void A_copy_follows_its_original_when_it_changes()
    {
        var path = Original("образец.md", "Первая версия");
        var entry = _guides.AddFile("Общие", GuideKindEnum.Example, path);

        Assert.Empty(_guides.RefreshFromOrigins());
        Assert.Equal("Первая версия", File.ReadAllText(entry.Path));

        File.WriteAllText(path, "Вторая версия, длиннее первой");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        Assert.Equal([entry.Title], _guides.RefreshFromOrigins().ToArray());
        Assert.Equal("Вторая версия, длиннее первой", File.ReadAllText(entry.Path));
        Assert.Contains("Обновлено из оригинала", _guides.Load().Single().Note, StringComparison.Ordinal);
        Assert.Empty(_guides.RefreshFromOrigins());

        File.Delete(path);

        Assert.Empty(_guides.RefreshFromOrigins());
        Assert.Equal("Вторая версия, длиннее первой", File.ReadAllText(entry.Path));
    }

    /// <summary>
    /// Сборка материалов сама подтягивает изменённый оригинал, а занятый оригинал не ломает сборку
    /// </summary>
    [Fact]
    public void Collecting_materials_picks_up_a_changed_original_and_survives_a_locked_one()
    {
        var path = Original("указания.md", "Старое");
        _guides.AddFile("Общие", GuideKindEnum.Guide, path);
        File.WriteAllText(path, "Новое и длиннее");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        var materials = _guides.Materials("Гейм-дизайн");

        Assert.Equal("Новое и длиннее", File.ReadAllText(materials.Guides.Single()));

        File.WriteAllText(path, "Ещё новее и ещё длиннее");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(2));
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        Assert.Equal("Новое и длиннее", File.ReadAllText(_guides.Materials("Гейм-дизайн").Guides.Single()));
    }

    /// <summary>
    /// В окне «Уточнить» отмечены материалы, которые к предмету относятся, а ключи снятых галочек уходят в исключения
    /// </summary>
    [Fact]
    public void The_refine_dialog_returns_the_keys_of_unchecked_materials()
    {
        var concept = _guides.AddFile("Общие", GuideKindEnum.Guide, Original("концепция.md", "Концепция"));
        var example = _guides.AddFile("Общие", GuideKindEnum.Example, Original("образец.md", "Образец"));
        var dialog = new SubjectGuidesDialogViewModel("Иностранный язык", _guides.Load(), [concept.Id], _guides.Folder);

        Assert.False(dialog.Guides.Single().IsChecked);
        Assert.True(dialog.Examples.Single().IsChecked);
        Assert.False(dialog.NoMaterials);

        dialog.Examples.Single().IsChecked = false;
        dialog.Guides.Single().IsChecked = true;

        Assert.Equal([example.Id], dialog.Excluded().ToArray());
        Assert.True(new SubjectGuidesDialogViewModel("П", [], [], _guides.Folder).NoMaterials);
    }
}
