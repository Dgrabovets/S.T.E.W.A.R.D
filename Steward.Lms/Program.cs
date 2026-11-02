using System.Globalization;
using System.Text.Json;
using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Homework.Storage;
using Steward.Lms.Common;
using Steward.Lms.Models;
using Steward.Lms.Services;
using Steward.Status.Common;
using Steward.Status.Progress;
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
    /// Шаги обычного запуска по порядку: их видно на странице «Статус», пока процесс работает
    /// </summary>
    private static readonly string[] Stages = ["Занятия", "Расшифровки", "Разбор", "Задания", "Работы", "Заметки"];

    /// <summary>
    /// Прогресс обычного запуска. Пусто у остальных команд: им шаги не нужны
    /// </summary>
    private static RunProgressReporter? _progress;

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
                case "page":
                    if (args.Length < 3 || !Uri.TryCreate(args[1], UriKind.Absolute, out var pageUrl) || !string.Equals(pageUrl.Host, new Uri(settings.BaseUrl).Host, StringComparison.OrdinalIgnoreCase))
                    {
                        Log("Использование: page <адрес страницы на сайте LMS> <файл>");

                        return 2;
                    }

                    using (var lms = new LmsClient(settings))
                    {
                        await lms.LoginAsync(CancellationToken.None);
                        File.WriteAllText(args[2], await lms.PageAsync(args[1], CancellationToken.None));
                    }

                    return 0;
                case "extract":
                    Log(LmsItems(new HomeworkStore(Path.Combine(DataLocation.DataDirectory(), "homework"))));

                    return 0;
                case "prepare":
                    Log((await PrepareAsync(settings, new HomeworkStore(Path.Combine(DataLocation.DataDirectory(), "homework")))).Summary);

                    return 0;
                case "task":
                    if (args.Length < 3 || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    {
                        Log("Использование: task <ключ занятия> <номер задания>");

                        return 2;
                    }

                    Log(await AddTaskAsync(settings, new HomeworkStore(Path.Combine(DataLocation.DataDirectory(), "homework")), args[1], int.Parse(args[2], CultureInfo.InvariantCulture)));

                    return 0;
                case "note-task":
                    if (args.Length < 3 || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    {
                        Log("Использование: note-task <ключ занятия> <номер задания в разборе>");

                        return 2;
                    }

                    Log(await AddQuoteTaskAsync(settings, new HomeworkStore(Path.Combine(DataLocation.DataDirectory(), "homework")), args[1], int.Parse(args[2], CultureInfo.InvariantCulture)));

                    return 0;
                case "analyze":
                    Log(await AnalyzeAsync(settings, new HomeworkStore(Path.Combine(DataLocation.DataDirectory(), "homework"))));

                    return 0;
                case "publish":
                    Log(await PublishAsync(settings, new HomeworkStore(Path.Combine(DataLocation.DataDirectory(), "homework"))));

                    return 0;
                case "probe":
                    if (args.Length < 2)
                    {
                        Log("Использование: probe <адрес>");

                        return 2;
                    }

                    foreach (var line in await browser.ProbeAsync(args[1], CancellationToken.None))
                        Log(line);

                    return 0;
                case "get":
                    if (args.Length < 2)
                    {
                        Log("Использование: get <адрес> [параметры]");

                        return 2;
                    }

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
                    _progress = new RunProgressReporter(new RunProgressStore(DataLocation.StatusDirectory()), STATUS_ID, Stages);
                    _progress.Enter(0, 0, "Список занятий в LMS");
                    var summary = await SyncAsync(settings, store);
                    _progress.Enter(1, 0, "Запись занятия из MTS Link");
                    summary += "\n" + await FetchAsync(browser, settings, store, settings.MaxFetchPerRun);
                    _progress.Enter(0, 0, "Работы и сроки в LMS");
                    summary += "\n" + await TasksAsync(settings, store, dryRun: !settings.CreateTasks);
                    _progress.Enter(2, 0, "Разбор занятий");
                    summary += "\n" + await AnalyzeAsync(settings, store);
                    _progress.Enter(3, 0, "Задания из LMS");
                    summary += "\n" + LmsItems(store);
                    _progress.Enter(4, 0, "Подготовка работ");
                    var (prepareSummary, notices) = await PrepareAsync(settings, store);
                    summary += "\n" + prepareSummary;
                    _progress.Enter(5, 0, "Заметки и работы в Singularity");
                    summary += "\n" + await PublishSafelyAsync(settings, store);
                    Log(summary);

                    // Строки «Готова работа: …» уже напечатаны по мере готовности; в сводку статуса они идут для уведомления в трее
                    Report(statusStore, ProcessStateEnum.Ok, notices.Count > 0 ? summary + "\n" + string.Join("\n", notices) : summary, null);
                    _progress.Finish();

                    return 0;
            }
        }
        catch (Exception exception)
        {
            Log($"Ошибка: {exception}");
            Report(statusStore, ProcessStateEnum.Failed, null, Describe(exception));
            _progress?.Finish();

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
        foreach (var subject in activities.GroupBy(activity => activity.Subject).OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var assignments = subject.Where(activity => activity.Kind == ActivityKindEnum.Assignment).ToArray();
            var quizzes = subject.Where(activity => activity.Kind == ActivityKindEnum.Quiz).ToArray();
            Log($"LMS: {subject.Key}: заданий {assignments.Length}, сдано {assignments.Count(activity => activity.Done)}; тестов {quizzes.Length}, пройдено {quizzes.Count(activity => activity.Done)}");
        }

        await AssignmentSync.SyncAsync(lms, store, activities, Log);
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

        _progress?.Enter(1, wanted.Count, "Запись занятия из MTS Link");
        var fetched = 0;

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
                    _progress?.Step(++fetched, $"{lesson.Title}, {lesson.Date:dd.MM.yyyy}");

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
        _progress?.Enter(2, todo.Length, "Разбор занятий");
        foreach (var lesson in todo)
        {
            _progress?.Step(done, $"{lesson.Title}, {lesson.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}");
            var path = store.AnalysisPath(lesson.Id);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var error = await ClaudeAgent.RunLessonAsync(LessonAnalysisPrompt.Build(lesson, store.TranscriptPath(lesson.Id), path, settings.StudentName), store.Root, [], CancellationToken.None);
            var analysis = error.Length == 0 ? store.ReadAnalysis(lesson.Id) : null;
            if (error.Length == 0 && analysis is null)
                error = "Claude записал разбор в неверном формате";

            if (analysis is not null)
            {
                // К каждому заданию дописываются реплики до и после слов преподавателя: сохраняется только этот отрывок, а не вся расшифровка
                var temp = $"{path}.{Guid.NewGuid():N}.tmp";
                try
                {
                    File.WriteAllText(temp, analysis.WithExcerpts(File.ReadAllText(store.TranscriptPath(lesson.Id))).ToJson());
                    File.Move(temp, path, overwrite: true);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // Отрывки это дополнение: любой сбой при их сборке оставляет разбор агента как есть, он годится и без них
                    Log($"Не удалось сохранить отрывки заданий: {lesson.Title}, {lesson.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}: {exception.Message}");
                    File.Delete(temp);
                }
            }

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
    /// Добавляет задания из LMS: работы, у которых там явно написано условие или приложен файл, копируются в карточки без агента. Из расшифровок задания сами не берутся: владелец добавляет их кнопкой «Добавить в задание» на вкладке «Задания» читалки
    /// </summary>
    /// <param name="store">Папка домашних заданий</param>
    /// <returns>Строка итога для статуса</returns>
    private static string LmsItems(HomeworkStore store)
    {
        var added = SyncLmsItems(store);

        return added > 0 ? $"Задания из LMS: добавлено {added}" : "Задания из LMS: новых нет";
    }

    /// <summary>
    /// Строит карточки заданий для предметов, у которых задания лежат в LMS: без агента, по работам LMS. Сбой записи не роняет запуск
    /// </summary>
    /// <param name="store">Папка домашних заданий</param>
    /// <returns>Сколько заданий добавлено</returns>
    private static int SyncLmsItems(HomeworkStore store)
    {
        try
        {
            var added = store.SyncLmsItems(store.LoadLessons());
            if (added > 0)
                Log($"Задания из LMS: добавлено {added}");

            return added;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log($"Задания из LMS не записались: {exception.Message}");

            return 0;
        }
    }

    /// <summary>
    /// Готовит работы по найденным несданным заданиям занятий не старше <see cref="LmsSettings.PrepareMaxAgeDays"/> дней, не больше <see cref="LmsSettings.MaxPreparePerRun"/> за запуск и не дольше <see cref="LmsSettings.PrepareBudgetMinutes"/>, и сразу их проверяет.
    /// По каждой готовой работе сразу пишет строку «Готова работа: предмет, задание». Если срок вышел, агент останавливается, готовое сохраняется, а запуск идёт дальше как обычный
    /// </summary>
    /// <param name="settings">Настройки: сколько работ готовить и сколько минут даётся шагу</param>
    /// <param name="store">Папка домашних заданий</param>
    /// <returns>Строка итога для статуса и строки уведомления о готовых работах для сводки статуса</returns>
    private static async Task<(string Summary, IReadOnlyList<string> Notices)> PrepareAsync(LmsSettings settings, HomeworkStore store)
    {
        var preparer = new HomeworkPreparer(store, new GuideStore(store.Root), new RemarkStore(Path.Combine(store.Root, "remarks")), ClaudeAgent.RunAsync);
        var budget = TimeSpan.FromMinutes(Math.Max(settings.PrepareBudgetMinutes, 1));
        var ready = 0;
        _progress?.Enter(4, Math.Max(settings.MaxPreparePerRun, 0), "Агент готовит работы");
        var report = await preparer.RunWithBudgetAsync(settings.MaxPreparePerRun, budget, work =>
        {
            Log(HomeworkPreparer.NOTICE + work);
            _progress?.Step(++ready, work);
        }, CancellationToken.None, settings.PrepareMaxAgeDays);
        if (report.Busy)
            return ("Подготовка работ: уже идёт в другом запуске", []);

        foreach (var line in report.Waiting)
            Log($"Ждёт владельца: {line}");

        foreach (var line in report.Failed)
            Log($"Подготовка не удалась: {line}");

        if (report.Cancelled)
            Log($"Подготовка работ остановлена: вышло время шага ({budget.TotalMinutes:0} мин), остальное в следующем запуске");

        var summary = $"Подготовка работ: готово {report.Prepared.Count}, ждут {report.Waiting.Count}, ошибок {report.Failed.Count}{(report.Cancelled ? ", остановлена по сроку" : string.Empty)}";

        return (summary, report.Notices());
    }

    /// <summary>
    /// Заводит задачу SingularityApp по найденному заданию по просьбе пользователя: кнопка «Добавить в Singularity» в окне. Флаг <c>CreateTasks</c> здесь не нужен, потому что запись просит сам человек
    /// </summary>
    /// <param name="settings">Настройки: подключение к SingularityApp</param>
    /// <param name="store">Папка домашних заданий</param>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <returns>Строка итога</returns>
    /// <exception cref="InvalidOperationException">Занятия или задания нет, либо Singularity не настроен</exception>
    private static async Task<string> AddTaskAsync(LmsSettings settings, HomeworkStore store, string lessonId, int number)
    {
        if (settings.Singularity is not { Token.Length: > 0 } configured || configured.Token.StartsWith('<'))
            throw new InvalidOperationException("Singularity не настроен: впиши токен в lmssettings.json");

        var lesson = store.LoadLessons().FirstOrDefault(item => item.Id == lessonId) ?? throw new InvalidOperationException("Занятие не найдено");
        var items = store.ReadItems(lessonId) ?? throw new InvalidOperationException("У занятия нет найденных заданий");
        if (number < 1 || number > items.Count)
            throw new InvalidOperationException("Такого задания нет");

        var externalId = HomeworkTaskBuilder.ExternalId(lesson, number);
        using var singularity = new Steward.Mail.Services.SingularityClient(configured);
        var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
        var known = await singularity.ListExternalIdsAsync(new DateTimeOffset(lesson.Date.AddDays(-1).ToDateTime(TimeOnly.MinValue), offset), CancellationToken.None);

        // Задача могла быть заведена кнопкой «Только в Singularity» по тому же заданию: тогда вторая не создаётся
        var remembered = store.LoadCreated();
        var exists = store.SingularityKeysForItem(lesson, number).Any(key => known.Contains(key) || remembered.Contains(key));
        if (!exists)
        {
            var drafts = Directory.Exists(store.DraftsDirectory(lessonId)) ? Directory.EnumerateFiles(store.DraftsDirectory(lessonId), $"Задание {number}.*").Order(StringComparer.Ordinal).ToArray() : [];
            await singularity.CreateTaskAsync(HomeworkTaskBuilder.Build(lesson, items[number - 1], number, store.FormatFor(lessonId), configured.ProjectId, offset, drafts), CancellationToken.None);
        }

        try
        {
            store.MarkCreated([externalId]);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Задача уже в Singularity: повтор её не заведёт, потому что ключ найдётся в списке задач
            Log($"Отметка о задаче не записалась: {exception.Message}");
        }

        return exists ? $"Задача по заданию уже была в Singularity: {items[number - 1].Title}" : $"Задача заведена в Singularity: {items[number - 1].Title}";
    }

    /// <summary>
    /// Заводит задачу SingularityApp по заданию из отрывка расшифровки, не добавляя его в список домашних заданий: кнопка «Только в Singularity» на вкладке «Задания» читалки. Флаг <c>CreateTasks</c> не нужен, потому что запись просит сам человек
    /// </summary>
    /// <param name="settings">Настройки: подключение к SingularityApp</param>
    /// <param name="store">Папка домашних заданий</param>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="index">Номер задания в разборе занятия, с единицы</param>
    /// <returns>Строка итога</returns>
    /// <exception cref="InvalidOperationException">Занятия или задания нет, либо Singularity не настроен</exception>
    private static async Task<string> AddQuoteTaskAsync(LmsSettings settings, HomeworkStore store, string lessonId, int index)
    {
        if (settings.Singularity is not { Token.Length: > 0 } configured || configured.Token.StartsWith('<'))
            throw new InvalidOperationException("Singularity не настроен: впиши токен в lmssettings.json");

        var lesson = store.LoadLessons().FirstOrDefault(item => item.Id == lessonId) ?? throw new InvalidOperationException("Занятие не найдено");
        var tasks = store.ReadAnalysis(lessonId)?.Tasks ?? [];
        if (index < 1 || index > tasks.Count)
            throw new InvalidOperationException("Такого задания в разборе занятия нет");

        var task = tasks[index - 1];
        var externalId = HomeworkTaskBuilder.QuoteExternalId(lesson, index);
        using var singularity = new Steward.Mail.Services.SingularityClient(configured);
        var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
        var known = await singularity.ListExternalIdsAsync(new DateTimeOffset(lesson.Date.AddDays(-1).ToDateTime(TimeOnly.MinValue), offset), CancellationToken.None);

        // Задача могла быть заведена и из списка заданий (кнопкой карточки или фоновым шагом): тогда вторая не создаётся
        var remembered = store.LoadCreated();
        if (store.SingularityKeysForQuote(lesson, index).Any(key => known.Contains(key) || remembered.Contains(key)))
            return $"Задача по заданию уже была в Singularity: {task.Title}";

        await singularity.CreateTaskAsync(HomeworkTaskBuilder.BuildFromQuote(lesson, task, index, configured.ProjectId, offset), CancellationToken.None);
        try
        {
            store.MarkCreated([externalId]);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Задача уже в Singularity: повтор её не заведёт, потому что ключ найдётся в списке задач
            Log($"Отметка о задаче не записалась: {exception.Message}");
        }

        return $"Задача заведена в Singularity: {task.Title}";
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
        var published = store.LoadCreated().ToHashSet(StringComparer.Ordinal);
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

            try
            {
                store.MarkCreated(["note:" + lesson.Id]);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Заметка уже записана: следующий запуск обновит её, а не заведёт вторую
                Log($"Отметка о заметке не записалась: {exception.Message}");
            }

            written++;
        }

        // Сетевые вызовы ограничены по времени; при выключенной записи (CreateTasks=false) клиент не вызывается, в журнал идут строки «Завёл бы»
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var dryRun = !settings.CreateTasks;
        var (created, _) = await new HomeworkTaskCreator(store, singularity, configured.ProjectId, TimeProvider.System, Log, dryRun).RunAsync(timeout.Token);
        written += created;
        written += await new HomeworkWorkLinker(store, singularity, TimeProvider.System, Log, dryRun).RunAsync(timeout.Token);

        return settings.CreateTasks ? $"Заметки и работы в Singularity: записано {written}" : "Заметки и работы: запись в Singularity выключена";
    }

    /// <summary>
    /// Публикует заметки и работы так, что сбой или истёкший срок публикации не роняет запуск: итог статуса с уведомлениями о готовых работах всё равно записывается
    /// </summary>
    /// <param name="settings">Настройки: подключение к SingularityApp и разрешение на запись</param>
    /// <param name="store">Папка домашних заданий</param>
    /// <returns>Строка итога для статуса</returns>
    private static async Task<string> PublishSafelyAsync(LmsSettings settings, HomeworkStore store)
    {
        try
        {
            return await PublishAsync(settings, store);
        }
        catch (OperationCanceledException)
        {
            Log("Заметки и работы: вышло время публикации (5 мин), остальное в следующем запуске");

            return "Заметки и работы: остановлено по сроку";
        }
        catch (Exception exception)
        {
            Log($"Заметки и работы: не удалось: {exception.GetType().Name}: {exception.Message}");

            return "Заметки и работы: ошибка, повтор в следующем запуске";
        }
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
            store.Report(STATUS_ID, "Автоматизация образования", state, DateTimeOffset.Now, summary, error, success: state != ProcessStateEnum.Failed);
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
