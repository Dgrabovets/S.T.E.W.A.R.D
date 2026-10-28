using System.Globalization;
using System.Text.Json;
using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Homework.Storage;
using Steward.Lms.Common;
using Steward.Lms.Models;
using Steward.Lms.Services;
using Steward.Status.Common;
using Steward.Status.State;

namespace Steward.Lms;

/// <summary>
/// Выгружает занятия из LMS и расшифровки из MTS Link для домашних заданий. Команды: без аргументов, <c>sync</c>, <c>install</c>, <c>login</c>
/// </summary>
internal static class Program
{
    /// <summary>
    /// Идентификатор процесса в общем формате статусов и в config/jobs.json
    /// </summary>
    private const string STATUS_ID = "lms";

    /// <summary>
    /// Выполняет команду: по умолчанию выгружает занятия и записывает итог в общий статус
    /// </summary>
    /// <param name="args">Команда: <c>sync</c>, <c>install</c> или <c>login</c>. Без аргументов выполняется <c>sync</c></param>
    /// <returns>0, если команда выполнена, и 1, если случилась ошибка</returns>
    private static async Task<int> Main(string[] args)
    {
        // Приложение в трее читает вывод как UTF-8, а консоль по умолчанию пишет в кодировке DOS
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);

        var command = args.FirstOrDefault() ?? "sync";
        var statusStore = new StatusStore(DataLocation.StatusDirectory());

        try
        {
            var settings = LoadSettings();
            var repoRoot = DataLocation.RepoRootOrStart();
            var browser = new MtsBrowser(settings, repoRoot);

            switch (command)
            {
                case "install":
                    Log($"Ставлю браузер в {browser.BrowsersPath}");

                    return browser.Install();
                case "tasks":
                    Log(await TasksAsync(settings, new HomeworkStore(Path.Combine(DataLocation.DataDirectory(), "homework")), dryRun: args.Contains("--dry-run")));

                    return 0;
                case "analyze":
                    Log(await AnalyzeAsync(settings, new HomeworkStore(Path.Combine(DataLocation.DataDirectory(), "homework"))));

                    return 0;
                case "publish":
                    Log(await PublishAsync(settings, new HomeworkStore(Path.Combine(DataLocation.DataDirectory(), "homework"))));

                    return 0;
                case "probe":
                    foreach (var line in await browser.ProbeAsync(args[1], CancellationToken.None))
                        Log(line);

                    return 0;
                case "get":
                    foreach (var line in await browser.GetAsync(args[1], args.Skip(2), CancellationToken.None))
                        Log(line);

                    return 0;
                case "login":
                    Log("Открываю окно MTS Link. Войди через SSO, окно закроется само");
                    var signedIn = await browser.SignInAsync(TimeSpan.FromMinutes(10), CancellationToken.None);
                    Log(signedIn ? "Вход в MTS Link выполнен и сохранён в файл" : "Вход не выполнен: окно закрыли или время вышло");

                    return signedIn ? 0 : 1;
                default:
                    var store = new HomeworkStore(Path.Combine(DataLocation.DataDirectory(), "homework"));
                    var summary = await SyncAsync(settings, store);
                    summary += "\n" + await FetchAsync(browser, settings, store, settings.MaxFetchPerRun);
                    summary += "\n" + await TasksAsync(settings, store, dryRun: !settings.CreateTasks);
                    summary += "\n" + await AnalyzeAsync(settings, store);
                    summary += "\n" + await PublishAsync(settings, store);
                    Log(summary);
                    Report(statusStore, ProcessStateEnum.Ok, summary, null);

                    return 0;
            }
        }
        catch (Exception exception)
        {
            Log($"Ошибка: {exception}");
            Report(statusStore, ProcessStateEnum.Failed, null, Describe(exception));

            return 1;
        }
    }

    /// <summary>
    /// Входит в LMS, читает курсы и записи вебинаров и обновляет список занятий. Новым занятиям ставит начальную отметку
    /// </summary>
    /// <param name="settings">Настройки входа</param>
    /// <param name="store">Папка домашних заданий</param>
    /// <returns>Строка итога для статуса</returns>
    private static async Task<string> SyncAsync(LmsSettings settings, HomeworkStore store)
    {
        using var lms = new LmsClient(settings);
        await lms.LoginAsync(CancellationToken.None);

        var lessons = new List<Lesson>();
        foreach (var course in await lms.CoursesAsync(CancellationToken.None))
            lessons.AddRange((await lms.WebinarsAsync(course, CancellationToken.None)).Select(LessonImporter.ToLesson));

        lessons = [.. lessons.DistinctBy(lesson => lesson.Id)];
        var known = store.LoadStates();
        var fresh = lessons.Where(lesson => !known.ContainsKey(lesson.Id)).ToArray();

        store.SaveLessons(lessons);
        store.SetStates(fresh.Select(lesson => new KeyValuePair<string, LessonStateEnum>(lesson.Id, LessonImporter.InitialState(lesson, settings.MinLessonMinutes))));

        return $"Занятий: {lessons.Count}\nНовых: {fresh.Length}";
    }

    /// <summary>
    /// Читает из LMS задания и тесты со статусами и сверяет их с задачами SingularityApp: заводит задачу на несданную работу предмета с отметкой «расшифровывать» и закрывает задачу, когда работа сдана
    /// </summary>
    /// <param name="settings">Настройки входа и SingularityApp</param>
    /// <param name="store">Папка домашних заданий</param>
    /// <param name="dryRun">Только показать, что было бы сделано, ничего не записывая в SingularityApp</param>
    /// <returns>Строка итога для статуса</returns>
    private static async Task<string> TasksAsync(LmsSettings settings, HomeworkStore store, bool dryRun)
    {
        using var lms = new LmsClient(settings);
        await lms.LoginAsync(CancellationToken.None);

        var activities = new List<LmsActivity>();
        foreach (var course in await lms.CoursesAsync(CancellationToken.None))
            activities.AddRange(await lms.ActivitiesAsync(course, CancellationToken.None));

        ActivityStore.Save(store.Root, activities);
        foreach (var subject in activities.Where(activity => !activity.Done).GroupBy(activity => activity.Subject).OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
            Log($"Не сдано: {subject.Key}: {subject.Count()} ({store.SubjectStateFor(subject.Key)})");

        if (settings.Singularity is not { Token.Length: > 0 } || settings.Singularity.Token.StartsWith('<'))
            return $"Работ в LMS: {activities.Count}, не сдано: {activities.Count(activity => !activity.Done)}. Singularity не настроен, задачи не заводятся";

        using var singularity = new Steward.Mail.Services.SingularityClient(settings.Singularity);
        var tasks = await singularity.ListTasksAsync(DateTimeOffset.Now.AddYears(-1), "lms:", CancellationToken.None);
        var plan = ActivityTaskSync.Build(activities, tasks, store.SubjectStateFor);

        var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
        foreach (var activity in plan.ToCreate)
        {
            Log($"{(dryRun ? "Завёл бы" : "Завожу")} задачу: {activity.Name} ({activity.Subject}), срок {activity.Due:dd.MM.yyyy}");
            if (!dryRun)
                await singularity.CreateTaskAsync(ActivityTaskSync.Build(activity, settings.Singularity.ProjectId, DateOnly.FromDateTime(DateTime.Now), offset), CancellationToken.None);
        }

        foreach (var (task, activity) in plan.ToClose)
        {
            Log($"{(dryRun ? "Закрыл бы" : "Закрываю")} задачу: {activity.Name} ({activity.Subject}): сдано в LMS");
            if (!dryRun)
                await singularity.CompleteTaskAsync(task.Id, CancellationToken.None);
        }

        return $"Работ в LMS: {activities.Count}, не сдано: {activities.Count(activity => !activity.Done)}\nЗадач {(dryRun ? "завёл бы" : "заведено")}: {plan.ToCreate.Count}, {(dryRun ? "закрыл бы" : "закрыто")}: {plan.ToClose.Count}";
    }

    /// <summary>
    /// Забирает расшифровки занятий: сначала заказанные по кнопке «Расшифровать», потом занятия предметов с отметкой «расшифровывать», от свежих к старым. Кладёт их в <c>transcripts</c> и записывает заметку с резюме в SingularityApp
    /// </summary>
    /// <param name="browser">Браузер MTS Link</param>
    /// <param name="settings">Настройки: подключение к SingularityApp и разрешение на запись</param>
    /// <param name="store">Папка домашних заданий</param>
    /// <param name="maxPerRun">Сколько расшифровок забирать за один запуск: остальные подтянутся постепенно в следующих</param>
    /// <returns>Строка итога для статуса</returns>
    private static async Task<string> FetchAsync(MtsBrowser browser, LmsSettings settings, HomeworkStore store, int maxPerRun)
    {
        var wanted = store.LoadLessons()
            .Where(store.ShouldFetch)
            .OrderByDescending(lesson => store.StateFor(lesson.Id) == LessonStateEnum.Fetch)
            .Take(maxPerRun)
            .ToDictionary(lesson => lesson.Id, StringComparer.Ordinal);
        if (wanted.Count == 0)
            return "Расшифровки: новых к выгрузке нет";

        var singularity = settings.Singularity is { Token.Length: > 0 } configured && !configured.Token.StartsWith('<') ? new Steward.Mail.Services.SingularityClient(configured) : null;
        try
        {
            var noted = singularity is null
                ? new HashSet<string>(StringComparer.Ordinal)
                : (await singularity.ListTasksAsync(DateTimeOffset.Now.AddYears(-2), TranscriptNote.KEY_PREFIX, CancellationToken.None)).Select(task => task.ExternalId).ToHashSet(StringComparer.Ordinal);
            var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);

            var received = await browser.FetchAsync(
                wanted.ToDictionary(pair => pair.Key, pair => pair.Value.RecordUrl, StringComparer.Ordinal),
                async (lessonId, transcript) =>
                {
                    var lesson = wanted[lessonId];
                    var path = store.TranscriptPath(lessonId);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, TranscriptFormatter.Format(lesson, transcript));
                    Log($"Расшифровка: {lesson.Title}, {lesson.Date:dd.MM.yyyy}");

                    if (singularity is null || noted.Contains(TranscriptNote.KEY_PREFIX + lessonId))
                        return;

                    Log($"{(settings.CreateTasks ? "Записываю" : "Записал бы")} заметку с расшифровкой в Singularity");
                    if (settings.CreateTasks)
                        await singularity.CreateTaskAsync(TranscriptNote.Build(lesson, transcript, path, settings.Singularity!.ProjectId, offset), CancellationToken.None);
                },
                CancellationToken.None);

            return $"Расшифровки: получено {received} из {wanted.Count}";
        }
        finally
        {
            singularity?.Dispose();
        }
    }

    /// <summary>
    /// Просит Claude разобрать занятия, у которых есть расшифровка, но нет разбора, и записывает разбор в <c>analysis</c>. Полный текст по ролям в разбор не попадает
    /// </summary>
    /// <param name="settings">Настройки: сколько занятий разбирать за запуск</param>
    /// <param name="store">Папка домашних заданий</param>
    /// <returns>Строка итога для статуса</returns>
    private static async Task<string> AnalyzeAsync(LmsSettings settings, HomeworkStore store)
    {
        var todo = store.LoadLessons().Where(store.ShouldAnalyze).Take(Math.Max(settings.MaxAnalyzePerRun, 0)).ToArray();
        if (todo.Length == 0)
            return "Разбор: новых занятий нет";

        var done = 0;
        foreach (var lesson in todo)
        {
            var path = store.AnalysisPath(lesson.Id);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var error = await ClaudeAgent.RunAsync(LessonAnalysisPrompt.Build(lesson, store.TranscriptPath(lesson.Id), path, settings.StudentName), store.Root, CancellationToken.None);
            if (error.Length == 0 && store.ReadAnalysis(lesson.Id) is null)
                error = "Claude записал разбор в неверном формате";

            if (error.Length > 0)
            {
                File.Delete(path);
                Log($"Разбор не удался: {lesson.Title}, {lesson.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}: {error}");

                continue;
            }

            Log($"Разбор: {lesson.Title}, {lesson.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}");
            done++;
        }

        return $"Разбор занятий: {done} из {todo.Length}";
    }

    /// <summary>
    /// Записывает в SingularityApp подробные разборы занятий в заметки и ссылки на готовые файлы заданий в задачи ДЗ. Пока запись выключена, только пишет в лог, что сделала бы. Singularity не принимает вложения, поэтому файлы остаются на диске, а в заметке лежат ссылки
    /// </summary>
    /// <param name="settings">Настройки: подключение к SingularityApp и разрешение на запись</param>
    /// <param name="store">Папка домашних заданий</param>
    /// <returns>Строка итога для статуса</returns>
    private static async Task<string> PublishAsync(LmsSettings settings, HomeworkStore store)
    {
        if (settings.Singularity is not { Token.Length: > 0 } configured || configured.Token.StartsWith('<'))
            return "Заметки: Singularity не настроен";

        var lessons = store.LoadLessons();
        if (lessons.Count == 0)
            return "Заметки: занятий нет";

        using var singularity = new Steward.Mail.Services.SingularityClient(configured);
        var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
        var from = new DateTimeOffset(lessons.Min(lesson => lesson.Date).AddDays(-1).ToDateTime(TimeOnly.MinValue), offset);
        var published = store.LoadCreated();
        var written = 0;

        var notes = (await singularity.ListTasksAsync(from, AnalysisNote.KEY_PREFIX, CancellationToken.None)).GroupBy(task => task.ExternalId).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var lesson in lessons.Where(lesson => !published.Contains("note:" + lesson.Id)))
        {
            if (store.ReadAnalysis(lesson.Id) is not { } analysis)
                continue;

            var key = AnalysisNote.KEY_PREFIX + lesson.Id;
            Log($"{(settings.CreateTasks ? "Записываю" : "Записал бы")} разбор занятия в Singularity: {lesson.Title}, {lesson.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}");
            if (!settings.CreateTasks)
                continue;

            if (notes.TryGetValue(key, out var existing))
                await singularity.UpdateNoteAsync(existing.Id, AnalysisNote.Text(lesson, analysis), CancellationToken.None);
            else
                await singularity.CreateTaskAsync(AnalysisNote.Build(lesson, analysis, configured.ProjectId, offset), CancellationToken.None);

            store.MarkCreated(["note:" + lesson.Id]);
            written++;
        }

        if (settings.CreateTasks)
            await new HomeworkTaskCreator(store, singularity, configured.ProjectId, TimeProvider.System, Log).RunAsync(CancellationToken.None);

        var homework = (await singularity.ListTasksAsync(from, "hw:", CancellationToken.None)).GroupBy(task => task.ExternalId).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var lesson in lessons.Where(store.PreparesHomework))
        {
            var items = store.ReadItems(lesson.Id);
            if (items is null || !Directory.Exists(store.DraftsDirectory(lesson.Id)))
                continue;

            for (var index = 0; index < items.Count; index++)
            {
                var files = Directory.EnumerateFiles(store.DraftsDirectory(lesson.Id), $"Задание {index + 1}.*").Order(StringComparer.Ordinal).ToArray();
                var mark = $"draft:{lesson.Id}:{index + 1}:{string.Join('|', files.Select(Path.GetFileName))}";
                if (files.Length == 0 || store.LoadCreated().Contains(mark) || !homework.TryGetValue(HomeworkTaskBuilder.ExternalId(lesson, index + 1), out var task))
                    continue;

                Log($"{(settings.CreateTasks ? "Добавляю" : "Добавил бы")} ссылку на готовую работу в задачу: {items[index].Title}");
                if (!settings.CreateTasks)
                    continue;

                await singularity.UpdateNoteAsync(task.Id, HomeworkTaskBuilder.Build(lesson, items[index], index + 1, store.FormatFor(lesson.Id), configured.ProjectId, offset, files).Note, CancellationToken.None);
                store.MarkCreated([mark]);
                written++;
            }
        }

        return settings.CreateTasks ? $"Заметки и работы в Singularity: записано {written}" : "Заметки и работы: запись в Singularity выключена";
    }

    /// <summary>
    /// Читает lmssettings.json рядом с программой
    /// </summary>
    /// <returns>Настройки входа</returns>
    /// <exception cref="InvalidOperationException">Файла нет или он не читается</exception>
    private static LmsSettings LoadSettings()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "lmssettings.json");
        if (!File.Exists(path))
            throw new InvalidOperationException("Нет файла lmssettings.json рядом с программой: скопируй lmssettings.example.json и впиши логин и пароль LMS");

        return JsonSerializer.Deserialize<LmsSettings>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidOperationException("Не удалось прочитать lmssettings.json");
    }

    /// <summary>
    /// Запускает установщик браузеров Playwright
    /// </summary>
    /// <returns>Код завершения установщика</returns>
    internal static int Install() => Microsoft.Playwright.Program.Main(["install", "chromium"]);

    /// <summary>
    /// Записывает итог в общую папку статусов
    /// </summary>
    /// <param name="store">Папка статусов</param>
    /// <param name="state">Итоговое состояние</param>
    /// <param name="summary">Сводка или <see langword="null"/> при ошибке</param>
    /// <param name="error">Текст ошибки или <see langword="null"/> при успехе</param>
    private static void Report(StatusStore store, ProcessStateEnum state, string? summary, string? error)
    {
        try
        {
            store.Report(STATUS_ID, "Занятия LMS", state, DateTimeOffset.Now, summary, error, success: state != ProcessStateEnum.Failed);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Папка статусов недоступна: итог уже в выводе
        }
    }

    /// <summary>
    /// Описывает ошибку цепочкой причин
    /// </summary>
    /// <param name="exception">Исключение</param>
    /// <returns>Строка вида «Тип: сообщение → …»</returns>
    private static string Describe(Exception exception)
    {
        var parts = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
            parts.Add($"{current.GetType().Name}: {current.Message}");

        return string.Join(" → ", parts);
    }

    /// <summary>
    /// Пишет строку с временем в вывод
    /// </summary>
    /// <param name="message">Текст строки</param>
    private static void Log(string message) => Console.WriteLine($"{DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture)}  {message}");
}
