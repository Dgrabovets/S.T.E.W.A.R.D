using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Homework.Storage;
using Steward.ViewModels;

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

    /// <summary>
    /// Хранилище домашних заданий в папке теста
    /// </summary>
    /// <returns>Хранилище</returns>
    private HomeworkStore Homework() => new(_root);

    /// <summary>
    /// Записывает занятие с двумя заданиями
    /// </summary>
    /// <returns>Занятие</returns>
    private Lesson AddLesson()
    {
        var lesson = new Lesson("l1", "Физика", new DateOnly(2026, 10, 20));
        var store = Homework();
        store.SaveLessons([lesson]);
        Directory.CreateDirectory(Path.Combine(_root, "items"));
        File.WriteAllText(store.ItemsPath("l1"), """[{"title": "Первое"}, {"title": "Второе"}]""");

        return lesson;
    }

    /// <summary>
    /// Подготовщик, агент которого не должен вызываться: он записывает вызовы
    /// </summary>
    /// <param name="prompts">Сюда пишутся запросы к агенту</param>
    /// <returns>Подготовщик</returns>
    private HomeworkPreparer Preparer(List<string> prompts) => new(Homework(), new GuideStore(_root), new RemarkStore(Path.Combine(_root, "remarks")), (prompt, _, _, _) =>
    {
        prompts.Add(prompt);

        return Task.FromResult("агент не должен вызываться");
    });

    /// <summary>
    /// Своя работа копируется как «Задание N.расширение» в папку занятия, расширение приводится к нижнему регистру, недописанных файлов не остаётся, а шаг становится «Готово»
    /// </summary>
    [Fact]
    public void Own_work_is_copied_as_the_task_file_and_marks_the_step_Ready()
    {
        var store = Homework();

        var path = store.AttachOwnWork("l1", 2, Source("Моя работа.DOCX", "текст"));

        Assert.Equal(Path.Combine(store.DraftsDirectory("l1"), "Задание 2.docx"), path);
        Assert.Equal("текст", File.ReadAllText(path));
        Assert.Equal(path, store.SampleCandidate("l1", 2));
        Assert.Equal([path], store.DraftFiles("l1"));
        Assert.Empty(Directory.GetFiles(store.DraftsDirectory("l1"), "*.partial"));
    }

    /// <summary>
    /// Прежние файлы этого задания уходят в <c>.previous</c> с содержимым, файлы других заданий (в том числе «Задание 10») остаются, а повторная замена не затирает прежнюю копию
    /// </summary>
    [Fact]
    public void An_existing_draft_moves_to_previous()
    {
        var store = Homework();
        var folder = store.DraftsDirectory("l1");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Задание 1.docx"), "агент");
        File.WriteAllText(Path.Combine(folder, "Задание 1. Название.pptx"), "презентация");
        File.WriteAllText(Path.Combine(folder, "Задание 10.docx"), "чужое десятое");
        File.WriteAllText(Path.Combine(folder, "Задание 2.docx"), "чужое второе");

        var first = store.AttachOwnWork("l1", 1, Source("a.docx", "моя первая"));
        var second = store.AttachOwnWork("l1", 1, Source("b.docx", "моя вторая"));

        var previous = Path.Combine(folder, ".previous");
        Assert.Equal("моя вторая", File.ReadAllText(second));
        Assert.Equal(first, second);
        Assert.Equal("чужое десятое", File.ReadAllText(Path.Combine(folder, "Задание 10.docx")));
        Assert.Equal("чужое второе", File.ReadAllText(Path.Combine(folder, "Задание 2.docx")));
        Assert.Equal("презентация", File.ReadAllText(Path.Combine(previous, "Задание 1. Название.pptx")));
        Assert.Equal(["агент", "моя первая"], Directory.GetFiles(previous, "Задание 1*.docx").Select(File.ReadAllText).Order().ToArray());
        Assert.DoesNotContain(store.DraftFiles("l1"), file => file.Contains(".previous", StringComparison.Ordinal));
        Assert.Single(store.DraftFiles("l1"), file => HomeworkStore.IsTaskFile(file, 1));
    }

    /// <summary>
    /// Нет файла, папка вместо файла, слишком большой файл, пустое или чужое расширение, неверный номер и недопустимый ключ занятия отклоняются понятным сообщением, а ничего в папке занятия не появляется
    /// </summary>
    [Fact]
    public void A_missing_or_huge_file_is_rejected_with_a_message()
    {
        var store = Homework();

        var missing = Assert.Throws<InvalidDataException>(() => store.AttachOwnWork("l1", 1, Path.Combine(_outside, "нет.docx")));
        var directory = Assert.Throws<InvalidDataException>(() => store.AttachOwnWork("l1", 1, _outside));
        var huge = Path.Combine(_outside, "огромный.docx");
        using (var stream = new FileStream(huge, FileMode.Create))
            stream.SetLength(HomeworkStore.OWN_WORK_MAX_BYTES + 1);

        var tooBig = Assert.Throws<InvalidDataException>(() => store.AttachOwnWork("l1", 1, huge));
        var noExtension = Assert.Throws<InvalidDataException>(() => store.AttachOwnWork("l1", 1, Source("README", "текст")));
        var foreign = Assert.Throws<InvalidDataException>(() => store.AttachOwnWork("l1", 1, Source("работа.тхт", "текст")));
        var longExtension = Assert.Throws<InvalidDataException>(() => store.AttachOwnWork("l1", 1, Source("a.extension12345", "текст")));
        var service = Assert.Throws<InvalidDataException>(() => store.AttachOwnWork("l1", 1, Source("x.tmp", "текст")));
        var badNumber = Assert.Throws<InvalidDataException>(() => store.AttachOwnWork("l1", 0, Source("ok.txt", "текст")));
        Assert.Throws<ArgumentException>(() => store.AttachOwnWork("../evil", 1, Source("ok2.txt", "текст")));

        Assert.Contains("не найден", missing.Message);
        Assert.Contains("не файл", directory.Message);
        Assert.Contains("50", tooBig.Message);
        Assert.Contains("расширен", noExtension.Message);
        Assert.Contains("расширен", foreign.Message);
        Assert.Contains("расширен", longExtension.Message);
        Assert.Contains("расширен", service.Message);
        Assert.Contains("номер", badNumber.Message);
        Assert.False(Directory.Exists(store.DraftsDirectory("l1")) && Directory.EnumerateFileSystemEntries(store.DraftsDirectory("l1")).Any());
        Assert.False(Directory.Exists(Path.Combine(_root, "evil")));
    }

    /// <summary>
    /// Файл ровно предельного размера принимается
    /// </summary>
    [Fact]
    public void A_file_of_exactly_the_limit_is_accepted()
    {
        var exact = Path.Combine(_outside, "ровно.bin");
        using (var stream = new FileStream(exact, FileMode.Create))
            stream.SetLength(HomeworkStore.OWN_WORK_MAX_BYTES);

        var path = Homework().AttachOwnWork("l1", 1, exact);

        Assert.Equal(HomeworkStore.OWN_WORK_MAX_BYTES, new FileInfo(path).Length);
    }

    /// <summary>
    /// Повторное прикрепление того же файла (и самого файла из папки занятия) ничего не дублирует и не уводит в <c>.previous</c>
    /// </summary>
    [Fact]
    public void Attaching_the_same_file_again_creates_no_duplicates()
    {
        var store = Homework();
        var source = Source("a.docx", "одно и то же");
        var path = store.AttachOwnWork("l1", 1, source);

        var again = store.AttachOwnWork("l1", 1, source);
        var itself = store.AttachOwnWork("l1", 1, path);

        Assert.Equal(path, again);
        Assert.Equal(path, itself);
        Assert.Equal("одно и то же", File.ReadAllText(path));
        Assert.False(Directory.Exists(Path.Combine(store.DraftsDirectory("l1"), ".previous")));
        Assert.Single(Directory.GetFiles(store.DraftsDirectory("l1")));
    }

    /// <summary>
    /// Если копирование сорвалось (файл занят), недописанного файла нет, а прежняя работа остаётся на месте
    /// </summary>
    [Fact]
    public void A_failed_copy_leaves_no_partial_file_and_keeps_the_previous_work()
    {
        var store = Homework();
        var existing = store.AttachOwnWork("l1", 1, Source("a.docx", "прежняя"));
        var source = Source("b.docx", "новая");

        using (new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.ThrowsAny<IOException>(() => store.AttachOwnWork("l1", 1, source));

        Assert.Equal("прежняя", File.ReadAllText(existing));
        Assert.Equal([existing], Directory.GetFiles(store.DraftsDirectory("l1")));
        Assert.False(Directory.Exists(Path.Combine(store.DraftsDirectory("l1"), ".previous")));
    }

    /// <summary>
    /// Пока идёт фоновая подготовка (файл блокировки занят), файл не прикрепляется: прерванная попытка удалила бы его как новый
    /// </summary>
    [Fact]
    public void Attaching_is_refused_while_a_background_prepare_is_running()
    {
        var lesson = AddLesson();
        var preparer = Preparer([]);
        using var hold = new FileStream(Path.Combine(_root, "prepare.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        var result = preparer.AttachOwnWork(lesson, 1, Source("a.docx", "текст"));

        Assert.Equal(string.Empty, result.Path);
        Assert.Equal("Идёт подготовка работ в фоне, прикрепи файл позже", result.Message);
        Assert.Null(Homework().SampleCandidate("l1", 1));
    }

    /// <summary>
    /// Блокировка снимается после прикрепления: следующий запуск подготовки не считается занятым
    /// </summary>
    [Fact]
    public async Task The_prepare_lock_is_released_after_attaching()
    {
        var lesson = AddLesson();
        var preparer = Preparer([]);

        var result = preparer.AttachOwnWork(lesson, 1, Source("a.docx", "текст"));

        Assert.NotEqual(string.Empty, result.Path);
        Assert.False((await preparer.RunAsync(1, CancellationToken.None)).Busy);
    }

    /// <summary>
    /// Прикреплённая работа не готовится заново и не удаляется автоподготовкой, даже если агент не справляется
    /// </summary>
    [Fact]
    public async Task Automatic_preparation_never_touches_an_attached_work()
    {
        var lesson = AddLesson();
        var store = Homework();
        store.SetSubjectState("Физика", LessonStateEnum.Fetch);
        store.SetSubjectFormat("Физика", HomeworkFormatEnum.Text);
        var prompts = new List<string>();
        var preparer = Preparer(prompts);
        var path = preparer.AttachOwnWork(lesson, 1, Source("a.docx", "моя работа")).Path;

        var report = await preparer.RunAsync(5, CancellationToken.None);

        Assert.Equal("моя работа", File.ReadAllText(path));
        Assert.DoesNotContain(prompts, prompt => prompt.Contains("Первое", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Prepared, work => work.Contains("Первое", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Failed, work => work.Contains("Первое", StringComparison.Ordinal));
    }

    /// <summary>
    /// Работа сданного задания сразу становится образцом, написанным владельцем; у несданного образца нет
    /// </summary>
    [Fact]
    public void An_attached_work_of_a_submitted_item_becomes_a_manual_sample()
    {
        var lesson = AddLesson();
        var preparer = Preparer([]);
        var guides = new GuideStore(_root);
        preparer.AttachOwnWork(lesson, 2, Source("b.docx", "не сдано"));
        Assert.Empty(guides.Load());

        Homework().SetSubmitted("l1", 1, true);
        var result = preparer.AttachOwnWork(lesson, 1, Source("a.docx", "сдано"));

        var sample = Assert.Single(guides.Load());
        Assert.Equal("sample:l1:1", sample.Id);
        Assert.True(sample.Manual);
        Assert.Equal("сдано", File.ReadAllText(sample.Path));
        Assert.NotEqual(string.Empty, result.Path);
    }

    /// <summary>
    /// Если образец не записался (IOException), работа всё равно прикреплена, а в сообщении предупреждение
    /// </summary>
    [Fact]
    public void A_sample_failure_does_not_undo_the_attachment()
    {
        var lesson = AddLesson();
        Homework().SetSubmitted("l1", 1, true);
        Directory.CreateDirectory(Path.Combine(_root, "samples"));
        File.WriteAllText(Path.Combine(_root, "samples", "Физика"), "файл вместо папки образцов");

        var result = Preparer([]).AttachOwnWork(lesson, 1, Source("a.docx", "сдано"));

        Assert.NotEqual(string.Empty, result.Path);
        Assert.True(File.Exists(result.Path));
        Assert.Contains("образец", result.Message);
    }

    /// <summary>
    /// Испорченный файл образцов не превращает прикрепление в неудачу: работа на месте (хранилища сами сбрасывают испорченный JSON, а случайный JsonException в RegisterOwnSample тоже даёт предупреждение)
    /// </summary>
    [Fact]
    public void A_corrupt_sample_list_does_not_fail_the_attachment()
    {
        var lesson = AddLesson();
        Homework().SetSubmitted("l1", 1, true);
        File.WriteAllText(Path.Combine(_root, "guides.json"), "{ не json");

        var result = Preparer([]).AttachOwnWork(lesson, 1, Source("a.docx", "сдано"));

        Assert.True(File.Exists(result.Path));
        Assert.NotEqual(string.Empty, result.Path);
    }

    /// <summary>
    /// Символьная ссылка отклоняется как ссылка (тест пропускается, если ссылку нельзя создать без прав), а обычный файл принимается
    /// </summary>
    [Fact]
    public void A_symlink_is_refused_but_an_ordinary_file_is_accepted()
    {
        var real = Source("real.docx", "настоящий");
        var link = Path.Combine(_outside, "link.docx");
        var store = Homework();

        Assert.Equal("настоящий", File.ReadAllText(store.AttachOwnWork("l1", 1, real)));

        try
        {
            File.CreateSymbolicLink(link, real);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        var refused = Assert.Throws<InvalidDataException>(() => store.AttachOwnWork("l1", 2, link));
        Assert.Contains("ссылка", refused.Message);
    }

    /// <summary>
    /// Перенос в <c>.previous</c> при переделке не затирает одноимённый файл, который там уже лежит
    /// </summary>
    [Fact]
    public void Moving_to_previous_keeps_a_same_named_earlier_version()
    {
        var store = Homework();
        var folder = store.DraftsDirectory("l1");
        Directory.CreateDirectory(Path.Combine(folder, ".previous"));
        File.WriteAllText(Path.Combine(folder, ".previous", "Задание 1.docx"), "очень старая");
        File.WriteAllText(Path.Combine(folder, "Задание 1.docx"), "прошлая");

        var moved = store.MoveDraftsToPrevious("l1");

        Assert.Single(moved);
        Assert.Equal("прошлая", File.ReadAllText(moved[0]));
        Assert.Equal("очень старая", File.ReadAllText(Path.Combine(folder, ".previous", "Задание 1.docx")));
        Assert.Empty(store.DraftFiles("l1"));
    }

    /// <summary>
    /// Файл, выросший во время копирования выше предела, не остаётся даже недописанным: копирование прерывается
    /// </summary>
    [Fact]
    public void Copy_stops_when_a_file_exceeds_the_limit()
    {
        var huge = Path.Combine(_outside, "x.bin");
        using (var stream = new FileStream(huge, FileMode.Create))
            stream.SetLength(HomeworkStore.OWN_WORK_MAX_BYTES + 1);

        var store = Homework();
        Assert.Throws<InvalidDataException>(() => store.AttachOwnWork("l1", 1, huge));
        Assert.False(Directory.Exists(store.DraftsDirectory("l1")) && Directory.EnumerateFileSystemEntries(store.DraftsDirectory("l1")).Any());
    }

    /// <summary>
    /// Неверный номер задания и занятие без найденных заданий отклоняются сообщением
    /// </summary>
    [Fact]
    public void Attaching_to_an_unknown_item_is_refused_with_a_message()
    {
        var lesson = AddLesson();
        var preparer = Preparer([]);

        var outOfRange = preparer.AttachOwnWork(lesson, 3, Source("a.docx", "текст"));
        var noItems = preparer.AttachOwnWork(new Lesson("l2", "Физика", new DateOnly(2026, 10, 21)), 1, Source("b.docx", "текст"));

        Assert.Equal(string.Empty, outOfRange.Path);
        Assert.Contains("нет", outOfRange.Message);
        Assert.Equal(string.Empty, noItems.Path);
        Assert.Contains("не найдены", noItems.Message);
        Assert.Null(Homework().SampleCandidate("l1", 3));
    }

    /// <summary>
    /// Модель окна копирует файл не в потоке окна, показывает итог в карточке занятия и обновляет список готовых файлов
    /// </summary>
    [Fact]
    public async Task The_view_model_attaches_off_the_ui_thread_and_shows_the_result()
    {
        var callerThread = Environment.CurrentManagedThreadId;
        var copyThread = -1;
        var drafts = new List<string>();
        var model = new HomeworkFormatsViewModel([(new Lesson("l1", "Физика", new DateOnly(2026, 10, 20)), HomeworkFormatEnum.Word)], (_, _) => { }, _ => true, (_, _) => Task.FromResult((0, string.Empty)))
        {
            DraftLister = _ => drafts,
            WorkAttacher = (id, number, file) =>
            {
                copyThread = Environment.CurrentManagedThreadId;
                drafts.Add(file);

                return ("Готовая работа прикреплена", false);
            },
        };
        var lesson = model.Lessons[0];

        await model.AttachWorkAsync(lesson, 1, "C:\\работа.docx");

        Assert.NotEqual(callerThread, copyThread);
        Assert.Equal("Готовая работа прикреплена", lesson.Status);
        Assert.False(lesson.Failed);
        Assert.Equal(["C:\\работа.docx"], lesson.Drafts.Select(draft => draft.Path));
    }

    /// <summary>
    /// Ошибка прикрепления, в том числе исключение ввода-вывода, показывается в карточке как неудача
    /// </summary>
    [Fact]
    public async Task The_view_model_shows_a_failure_and_survives_an_exception()
    {
        var model = new HomeworkFormatsViewModel([(new Lesson("l1", "Физика", new DateOnly(2026, 10, 20)), HomeworkFormatEnum.Word)], (_, _) => { }, _ => true, (_, _) => Task.FromResult((0, string.Empty)))
        {
            WorkAttacher = (_, number, _) => number == 1 ? ("Файл слишком большой", true) : throw new IOException("диск занят"),
        };
        var lesson = model.Lessons[0];

        await model.AttachWorkAsync(lesson, 1, "a.docx");
        Assert.True(lesson.Failed);
        Assert.Equal("Файл слишком большой", lesson.Status);

        await model.AttachWorkAsync(lesson, 2, "a.docx");
        Assert.True(lesson.Failed);
        Assert.Contains("диск занят", lesson.Status);
    }
}
