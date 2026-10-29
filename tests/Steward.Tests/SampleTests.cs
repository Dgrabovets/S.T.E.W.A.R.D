using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Homework.Storage;

namespace Steward.Tests;

/// <summary>
/// Сданные работы как образцы предмета: копирование, замена повтора, отбор для агента, безопасные имена и защита папки от правки агентом
/// </summary>
public class SampleTests : IDisposable
{
    /// <summary>
    /// Папка домашних заданий на время теста
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "steward-samples-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Папка с исходными файлами владельца на время теста
    /// </summary>
    private readonly string _outside = Path.Combine(Path.GetTempPath(), "steward-samples-src-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Создаёт папку с исходными файлами
    /// </summary>
    public SampleTests()
    {
        Directory.CreateDirectory(_outside);
    }

    /// <summary>
    /// Удаляет временные папки теста
    /// </summary>
    public void Dispose()
    {
        foreach (var folder in new[] { _root, _outside })
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Создаёт исходный файл с текстом
    /// </summary>
    /// <param name="name">Имя файла</param>
    /// <param name="text">Содержимое</param>
    /// <returns>Путь к файлу</returns>
    private string Source(string name, string text)
    {
        var path = Path.Combine(_outside, name);
        File.WriteAllText(path, text);

        return path;
    }

    /// <summary>
    /// Образец копируется в папку предмета и регистрируется один раз на задание: повтор заменяет запись и файл
    /// </summary>
    [Fact]
    public void A_sample_is_copied_and_registered_once_per_task()
    {
        var store = new GuideStore(_root);

        var first = store.AddSample("Физика", "l1", 2, Source("a.txt", "первый"), fromUser: false);
        var second = store.AddSample("Физика", "l1", 2, Source("b.txt", "второй"), fromUser: false);

        Assert.Equal("sample:l1:2", first.Id);
        Assert.Equal(GuideKindEnum.Example, first.Kind);
        var entry = Assert.Single(store.Load());
        Assert.Equal(second.Path, entry.Path);
        Assert.Equal("второй", File.ReadAllText(entry.Path));
        Assert.StartsWith(Path.Combine(_root, "samples") + Path.DirectorySeparatorChar, entry.Path, StringComparison.OrdinalIgnoreCase);
        Assert.Single(Directory.EnumerateFiles(Path.GetDirectoryName(entry.Path)!));
    }

    /// <summary>
    /// Файл владельца заменяет черновик как образец, а черновик позже не затирает файл владельца
    /// </summary>
    [Fact]
    public void A_user_file_replaces_the_draft_as_the_sample()
    {
        var store = new GuideStore(_root);
        store.AddSample("Физика", "l1", 1, Source("draft.docx", "черновик"), fromUser: false);

        var user = store.AddSample("Физика", "l1", 1, Source("mine.txt", "моя работа"), fromUser: true);
        var again = store.AddSample("Физика", "l1", 1, Source("draft2.txt", "новый черновик"), fromUser: false);

        var entry = Assert.Single(store.Load());
        Assert.True(entry.Manual);
        Assert.Equal(user.Path, entry.Path);
        Assert.Equal(user.Path, again.Path);
        Assert.Equal("моя работа", File.ReadAllText(entry.Path));
        Assert.Single(Directory.EnumerateFiles(Path.GetDirectoryName(entry.Path)!));
    }

    /// <summary>
    /// В материалы идут два последних автоматических образца предмета и все ручные
    /// </summary>
    [Fact]
    public void Materials_take_two_latest_samples_of_the_subject_plus_manual_ones()
    {
        var store = new GuideStore(_root);
        store.AddSample("Физика", "l1", 1, Source("1.txt", "один"), fromUser: true);
        store.AddSample("Физика", "l2", 1, Source("2.txt", "два"), fromUser: false);
        store.AddSample("Физика", "l3", 1, Source("3.txt", "три"), fromUser: false);
        store.AddSample("Физика", "l4", 1, Source("4.txt", "четыре"), fromUser: false);

        var texts = store.Materials("Физика").Examples.Select(File.ReadAllText).Order().ToArray();

        Assert.Equal(["один", "три", "четыре"], texts);
    }

    /// <summary>
    /// Образцы других предметов не попадают в материалы ни при каком выборе
    /// </summary>
    [Fact]
    public void Samples_of_other_subjects_are_never_used()
    {
        var store = new GuideStore(_root);
        var other = store.AddSample("Химия", "l1", 1, Source("x.txt", "чужой"), fromUser: true);

        Assert.Empty(store.Materials("Физика").Examples);
        Assert.Empty(store.Materials("Физика", new HashSet<string> { other.Id }).Examples);
        Assert.Single(store.Materials("Химия").Examples);
    }

    /// <summary>
    /// Выключенный образец пропускается, а предыдущий по дате занимает его место
    /// </summary>
    [Fact]
    public void Disabled_samples_are_skipped()
    {
        var store = new GuideStore(_root);
        store.AddSample("Физика", "l1", 1, Source("1.txt", "один"), fromUser: false);
        store.AddSample("Физика", "l2", 1, Source("2.txt", "два"), fromUser: false);
        var off = store.AddSample("Физика", "l3", 1, Source("3.txt", "три"), fromUser: false);
        store.SetEnabled(off.Id, false);

        Assert.Equal(["два", "один"], store.Materials("Физика").Examples.Select(File.ReadAllText).Order().ToArray());
    }

    /// <summary>
    /// Предмет с двоеточием, косой чертой и кириллицей получает допустимую папку внутри samples, похожие названия не сливаются, а ключ занятия не выводит файл за пределы папки
    /// </summary>
    [Fact]
    public void Subject_with_forbidden_characters_gets_a_valid_folder()
    {
        var store = new GuideStore(_root);
        var a = store.AddSample("Физика: механика/квант?", "l1", 1, Source("a.txt", "а"), fromUser: false);
        var b = store.AddSample("Физика механика квант", "l2", 1, Source("b.txt", "б"), fromUser: false);
        var escape = store.AddSample("Физика", "../../evil", 1, Source("c.txt", "в"), fromUser: false);

        var samples = Path.GetFullPath(Path.Combine(_root, "samples")) + Path.DirectorySeparatorChar;
        foreach (var path in new[] { a.Path, b.Path, escape.Path })
        {
            Assert.StartsWith(samples, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(path));
        }

        Assert.NotEqual(Path.GetDirectoryName(a.Path), Path.GetDirectoryName(b.Path));
        Assert.Equal(Path.Combine(_root, "samples", "Физика"), Path.GetDirectoryName(escape.Path));
    }

    /// <summary>
    /// Испорченный guides.json не роняет чтение материалов, а отсутствующий исходный файл даёт понятное исключение без следов
    /// </summary>
    [Fact]
    public void A_corrupt_list_and_a_missing_source_do_not_crash()
    {
        var store = new GuideStore(_root);
        Directory.CreateDirectory(store.Folder);
        File.WriteAllText(Path.Combine(store.Folder, "guides.json"), "{ не json");

        Assert.Empty(store.Materials("Физика").Examples);
        Assert.Throws<FileNotFoundException>(() => store.AddSample("Физика", "l1", 1, Path.Combine(_outside, "нет.txt"), fromUser: false));
        Assert.False(Directory.Exists(Path.Combine(_root, "samples")) && Directory.EnumerateFileSystemEntries(Path.Combine(_root, "samples"), "*", SearchOption.AllDirectories).Any(path => File.Exists(path)));
    }

    /// <summary>
    /// Записи guides.json без новых полей читаются как есть
    /// </summary>
    [Fact]
    public void An_old_guides_file_without_new_fields_still_loads()
    {
        var store = new GuideStore(_root);
        Directory.CreateDirectory(store.Folder);
        File.WriteAllText(Path.Combine(store.Folder, "guides.json"), """[{"id":"a1","subject":"Физика","kind":1,"title":"x","path":"","url":"","note":""}]""");

        var entry = Assert.Single(store.Load());
        Assert.False(entry.Manual);
        Assert.Null(entry.Added);
        Assert.True(entry.Enabled);
    }

    /// <summary>
    /// Кандидат в образцы: файл «Задание N» занятия, а без него ничего
    /// </summary>
    [Fact]
    public void The_candidate_is_the_task_file_of_the_lesson()
    {
        var homework = new HomeworkStore(_root);
        var folder = homework.DraftsDirectory("l1");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Задание 1. Эссе.docx"), "x");
        File.WriteAllText(Path.Combine(folder, "Задание 2.docx"), "два");
        File.WriteAllText(Path.Combine(folder, "Задание 2.docx.partial"), "хвост");
        File.WriteAllText(Path.Combine(folder, "Задание 22.docx"), "не то");
        File.WriteAllText(Path.Combine(folder, "Задание 4.tar.gz"), "архив");
        File.WriteAllText(Path.Combine(folder, "Задание 5.tar.gz.partial"), "хвост");

        Assert.Equal(Path.Combine(folder, "Задание 2.docx"), homework.SampleCandidate("l1", 2));
        Assert.Equal(Path.Combine(folder, "Задание 1. Эссе.docx"), homework.SampleCandidate("l1", 1));
        Assert.Null(homework.SampleCandidate("l1", 3));
        Assert.Equal(Path.Combine(folder, "Задание 4.tar.gz"), homework.SampleCandidate("l1", 4));
        Assert.Null(homework.SampleCandidate("l1", 5));
        Assert.Null(homework.SampleCandidate("нет такого", 1));
    }

    /// <summary>
    /// Агенту запрещена правка папок образцов и указаний в рабочей папке
    /// </summary>
    [Fact]
    public void The_agent_cannot_edit_samples_or_guides()
    {
        var rules = ClaudeAgent.Arguments("п", @"D:\data\homework", []);

        Assert.Contains("Edit(//d/data/homework/samples/**)", rules);
        Assert.Contains("Edit(//d/data/homework/guides/**)", rules);
    }

    /// <summary>
    /// Выключенный владельцем автоматический образец остаётся выключенным после повторной регистрации
    /// </summary>
    [Fact]
    public void A_disabled_sample_stays_disabled_when_registered_again()
    {
        var store = new GuideStore(_root);
        var first = store.AddSample("Физика", "l1", 1, Source("a.txt", "один"), fromUser: false);
        store.SetEnabled(first.Id, false);

        var again = store.AddSample("Физика", "l1", 1, Source("a2.txt", "другой текст"), fromUser: false);

        Assert.False(again.Enabled);
        Assert.False(Assert.Single(store.Load()).Enabled);
    }

    /// <summary>
    /// Повтор того же файла не обновляет дату и не двигает образец вперёд, изменённый файл обновляет
    /// </summary>
    [Fact]
    public void Re_adding_an_identical_file_keeps_the_added_date_but_a_changed_file_refreshes_it()
    {
        var store = new GuideStore(_root);
        var old = store.AddSample("Физика", "l1", 1, Source("a.txt", "старый"), fromUser: false);
        store.AddSample("Физика", "l2", 1, Source("b.txt", "два"), fromUser: false);
        store.AddSample("Физика", "l3", 1, Source("c.txt", "три"), fromUser: false);

        var same = store.AddSample("Физика", "l1", 1, Source("a2.txt", "старый"), fromUser: false);

        Assert.Equal(old.Added, same.Added);
        Assert.Equal(["два", "три"], store.Materials("Физика").Examples.Select(File.ReadAllText).Order().ToArray());

        var changed = store.AddSample("Физика", "l1", 1, Source("a3.txt", "новый"), fromUser: false);

        Assert.True(changed.Added > old.Added);
        Assert.Equal(["новый", "три"], store.Materials("Физика").Examples.Select(File.ReadAllText).Order().ToArray());
    }

    /// <summary>
    /// Если файла прежнего ручного образца уже нет, черновик всё равно регистрируется
    /// </summary>
    [Fact]
    public void A_draft_registers_when_the_manual_sample_file_is_gone()
    {
        var store = new GuideStore(_root);
        var manual = store.AddSample("Физика", "l1", 1, Source("m.txt", "мой"), fromUser: true);
        File.Delete(manual.Path);

        var draft = store.AddSample("Физика", "l1", 1, Source("d.txt", "черновик"), fromUser: false);

        Assert.False(draft.Manual);
        Assert.Equal("черновик", File.ReadAllText(Assert.Single(store.Load()).Path));
    }

    /// <summary>
    /// Параллельная регистрация образцов через разные экземпляры хранилища над одной папкой ничего не теряет
    /// </summary>
    [Fact]
    public void Parallel_registrations_lose_nothing()
    {
        const int count = 30;
        var sources = Enumerable.Range(0, count).Select(index => Source($"s{index}.txt", "текст " + index)).ToArray();

        Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = 16 }, index => new GuideStore(_root).AddSample("Физика", "l" + index, 1, sources[index], fromUser: false));

        var entries = new GuideStore(_root).Load();
        Assert.Equal(count, entries.Count);
        Assert.Equal(count, entries.Select(entry => entry.Id).Distinct().Count());
    }
}
