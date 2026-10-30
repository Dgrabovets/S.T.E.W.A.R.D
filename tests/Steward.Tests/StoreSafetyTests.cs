using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Homework.Storage;

namespace Steward.Tests;

/// <summary>
/// Файлы владельца не теряются при сбое чтения: испорченный откладывается в <c>.bad</c>, а занятый не перезаписывается пустым
/// </summary>
public class StoreSafetyTests : IDisposable
{
    /// <summary>
    /// Папка домашних заданий на время теста
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "steward-safety-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Создаёт папку теста
    /// </summary>
    public StoreSafetyTests() => Directory.CreateDirectory(_root);

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
    /// Пишет испорченный JSON в файл папки теста
    /// </summary>
    /// <param name="name">Путь внутри папки теста</param>
    /// <returns>Полный путь</returns>
    private string Corrupt(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{не json");

        return path;
    }

    /// <summary>
    /// Держит файл открытым без совместного доступа, проверяет, что изменение выбрасывает ошибку, затем — что файл не изменился и временных файлов нет
    /// </summary>
    /// <param name="path">Файл владельца</param>
    /// <param name="change">Изменение, которое читает и пишет файл</param>
    private void AssertLockedFileKept(string path, Action change)
    {
        var before = File.ReadAllBytes(path);
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.ThrowsAny<IOException>(change);

        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp"));
        Assert.False(File.Exists(path + ".bad"));
    }

    /// <summary>
    /// Испорченный список материалов откладывается в <c>guides.json.bad</c>, и новая запись проходит
    /// </summary>
    [Fact]
    public void A_corrupt_guide_list_is_moved_aside_and_a_new_write_works()
    {
        var path = Corrupt(Path.Combine("guides", "guides.json"));
        var guides = new GuideStore(_root);
        var source = Path.Combine(_root, "работа.txt");
        File.WriteAllText(source, "текст");

        guides.AddSample("Физика", "l1", 1, source, fromUser: true);

        Assert.Equal("{не json", File.ReadAllText(path + ".bad"));
        Assert.Single(guides.Load());
    }

    /// <summary>
    /// Занятый список материалов не перезаписывается пустым ни образцом, ни галочкой, ни удалением
    /// </summary>
    [Fact]
    public void A_locked_guide_list_is_not_overwritten()
    {
        var guides = new GuideStore(_root);
        var source = Path.Combine(_root, "работа.txt");
        File.WriteAllText(source, "текст");
        var entry = guides.AddSample("Физика", "l1", 1, source, fromUser: true);
        var path = Path.Combine(_root, "guides", "guides.json");

        AssertLockedFileKept(path, () => guides.AddSample("Физика", "l1", 2, source, fromUser: true));
        AssertLockedFileKept(path, () => guides.SetEnabled(entry.Id, false));
        AssertLockedFileKept(path, () => guides.Remove(entry.Id));
        Assert.Single(guides.Load());
    }

    /// <summary>
    /// Испорченные файлы отметок, подтверждений, правок и созданных задач откладываются в <c>.bad</c>, и новая запись проходит
    /// </summary>
    [Fact]
    public void Corrupt_homework_marks_are_moved_aside_and_new_writes_work()
    {
        var store = new HomeworkStore(_root);
        var states = Corrupt("item-states.json");
        var confirmed = Corrupt("item-confirmed.json");
        var overrides = Corrupt("item-overrides.json");
        var created = Corrupt("created.json");

        store.SetSubmitted("l1", 1, true);
        store.SetOverride("l1", 2, "Название", string.Empty);
        store.MarkCreated(["hw:l1:1"]);

        foreach (var path in new[] { states, confirmed, overrides, created })
            Assert.Equal("{не json", File.ReadAllText(path + ".bad"));

        Assert.Equal([1], store.SubmittedNumbers("l1"));
        Assert.Equal([2], store.ConfirmedNumbers("l1"));
        Assert.Contains("hw:l1:1", store.LoadCreated());
    }

    /// <summary>
    /// Занятые файлы отметок владельца не перезаписываются пустыми
    /// </summary>
    [Fact]
    public void Locked_homework_marks_are_not_overwritten()
    {
        var store = new HomeworkStore(_root);
        store.SetSubmitted("l1", 1, true);
        store.SetOverride("l1", 2, "Название", string.Empty);
        store.MarkCreated(["hw:l1:1"]);

        AssertLockedFileKept(Path.Combine(_root, "item-states.json"), () => store.SetArchived("l1", 3, true));
        AssertLockedFileKept(Path.Combine(_root, "item-confirmed.json"), () => store.Confirm("l1", 3));
        AssertLockedFileKept(Path.Combine(_root, "item-overrides.json"), () => store.SetOverride("l1", 3, "Другое", string.Empty));
        AssertLockedFileKept(Path.Combine(_root, "created.json"), () => store.MarkCreated(["hw:l1:2"]));

        Assert.Equal([1], store.SubmittedNumbers("l1"));
        Assert.Contains(2, store.ConfirmedNumbers("l1"));
        Assert.Contains("hw:l1:1", store.LoadCreated());
    }

    /// <summary>
    /// Отметки о созданных задачах из нескольких потоков не теряются: запись идёт под блокировкой с уникальными временными файлами
    /// </summary>
    [Fact]
    public async Task Parallel_created_marks_are_not_lost()
    {
        var store = new HomeworkStore(_root);

        await Task.WhenAll(Enumerable.Range(1, 24).Select(number => Task.Run(() => store.MarkCreated([$"hw:l1:{number}"]))));

        Assert.Equal(24, store.LoadCreated().Count);
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp"));
    }

    /// <summary>
    /// Испорченный учёт попыток откладывается в <c>.bad</c>, а подготовка идёт дальше; занятый учёт не перезаписывается и агент без засчитанной попытки не запускается
    /// </summary>
    [Fact]
    public async Task The_attempt_ledger_is_moved_aside_when_corrupt_and_kept_when_locked()
    {
        var store = new HomeworkStore(_root);
        store.SetSubjectState("Физика", LessonStateEnum.Fetch);
        store.SetSubjectFormat("Физика", HomeworkFormatEnum.Text);
        store.SaveLessons([new Lesson("l1", "Физика", new DateOnly(2026, 10, 20))]);
        Directory.CreateDirectory(Path.Combine(_root, "items"));
        File.WriteAllText(store.ItemsPath("l1"), """[{"title": "Эссе"}]""");
        var attempts = Corrupt("prepare-attempts.json");
        var runs = 0;
        var preparer = new HomeworkPreparer(store, new GuideStore(_root), new RemarkStore(Path.Combine(_root, "remarks")), (_, _, _, _) =>
        {
            runs++;

            return Task.FromResult("сбой");
        });

        await preparer.RunAsync(1, CancellationToken.None);

        Assert.Equal("{не json", File.ReadAllText(attempts + ".bad"));
        Assert.Equal(1, runs);
        Assert.Contains("l1:1", File.ReadAllText(attempts), StringComparison.Ordinal);

        var before = File.ReadAllBytes(attempts);
        PrepareReport locked;
        using (new FileStream(attempts, FileMode.Open, FileAccess.Read, FileShare.None))
            locked = await preparer.RunAsync(1, CancellationToken.None);

        Assert.Equal(1, runs);
        Assert.Contains(locked.Failed, line => line.Contains("учёт попыток недоступен", StringComparison.Ordinal));
        Assert.Equal(before, File.ReadAllBytes(attempts));
    }
}
