using Steward.Homework.Models;
using Steward.Homework.Storage;

namespace Steward.Homework.Services;

/// <summary>
/// Итог автоподготовки
/// </summary>
/// <param name="Prepared">Готовые работы: «предмет, задание»</param>
/// <param name="Waiting">Задания, которые ждут владельца: «предмет, задание: вопрос или причина»</param>
/// <param name="Failed">Ошибки: «предмет, задание: причина»</param>
/// <param name="Busy">Подготовка уже идёт в другом запуске, этот ничего не делал</param>
public sealed record PrepareReport(IReadOnlyList<string> Prepared, IReadOnlyList<string> Waiting, IReadOnlyList<string> Failed, bool Busy = false)
{
    /// <summary>
    /// Строки уведомления о готовых работах для вывода фоновой задачи
    /// </summary>
    /// <returns>Строки «Готова работа: предмет, задание»</returns>
    public IReadOnlyList<string> Notices() => [.. Prepared.Select(work => HomeworkPreparer.NOTICE + work)];
}

/// <summary>
/// Общая подготовка работ для фоновой задачи и кнопок приложения: выбор заданий, оценка уверенности, формат предмета, работа агента с указаниями, образцами и замечаниями, проверка готовой работы, лимит и повторы после ошибки
/// </summary>
/// <param name="store">Папка домашних заданий</param>
/// <param name="guides">Методические указания и образцы</param>
/// <param name="remarks">Замечания владельца (папка <c>remarks</c> внутри папки домашних заданий)</param>
/// <param name="agent">Запуск агента</param>
public sealed class HomeworkPreparer(HomeworkStore store, GuideStore guides, RemarkStore remarks, DraftBuilder.AgentRun agent)
{
    /// <summary>
    /// Начало строки уведомления о готовой работе в выводе фоновой задачи
    /// </summary>
    public const string NOTICE = "Готова работа: ";

    /// <summary>
    /// Сколько раз подряд агент может не справиться с заданием, дальше задание ждёт владельца
    /// </summary>
    public const int MAX_ATTEMPTS = 3;

    /// <summary>
    /// Файл, который держит открытым идущая подготовка: второй запуск в это время ничего не делает
    /// </summary>
    private const string LOCK_FILE = "prepare.lock";

    /// <summary>
    /// Учёт попыток и зарегистрированных образцов
    /// </summary>
    private readonly PrepareLedger _ledger = new(store.Root);

    /// <summary>
    /// Готовит работы по найденным заданиям: от свежих занятий к старым, только несданные, не в архиве, без файла работы, у предметов, которые готовятся, и не больше лимита за запуск.
    /// Задание, в котором Steward не уверен, ждёт подтверждения. После ошибки агента повтор на следующем запуске, не больше трёх раз подряд. Каждая новая работа сразу проверяется. Работа сданного задания один раз становится образцом предмета
    /// </summary>
    /// <param name="maxWorks">Сколько работ готовить за запуск, с удачными и неудачными попытками</param>
    /// <param name="cancellationToken">Отменяет запуск</param>
    /// <returns>Готовые, ждущие и неудачные задания</returns>
    /// <exception cref="OperationCanceledException">Запуск отменён</exception>
    public async Task<PrepareReport> RunAsync(int maxWorks, CancellationToken cancellationToken)
    {
        using var hold = TryHold();
        if (hold is null)
            return new PrepareReport([], [], [], Busy: true);

        var prepared = new List<string>();
        var waiting = new List<string>();
        var failed = new List<string>();
        var budget = Math.Max(maxWorks, 0);
        var doneWorks = store.LoadActivities().Where(work => work.Done && work.Id.Length > 0).Select(work => work.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var lesson in store.LoadLessons())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                budget = await RunLessonAsync(lesson, doneWorks, budget, prepared, waiting, failed, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException or System.Text.Json.JsonException)
            {
                failed.Add($"{lesson.Title}: {exception.Message}");
            }
        }

        return new PrepareReport(prepared, waiting, failed);
    }

    /// <summary>
    /// Готовит работы одного занятия по кнопке «Подготовить задание»: берёт выбранные материалы, условия из LMS и замечания владельца, а новую работу сразу проверяет
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="items">Задания занятия: сданные и архивные отмечены <see cref="HomeworkItem.Done"/></param>
    /// <param name="format">Формат работы</param>
    /// <param name="extra">Готовит дополнительное указание; вызывается, когда подготовка уже занята этим запуском (например, переносит прежнюю версию при переделке)</param>
    /// <param name="guideIds">Какие материалы взять или <see langword="null"/> для всех включённых</param>
    /// <param name="cancellationToken">Отменяет запуск</param>
    /// <returns>Пустая строка или причина, почему работы не готовы</returns>
    public async Task<string> PrepareLessonAsync(Lesson lesson, IReadOnlyList<HomeworkItem> items, HomeworkFormatEnum format, Func<string> extra, IReadOnlySet<string>? guideIds, CancellationToken cancellationToken)
    {
        using var hold = TryHold();
        if (hold is null)
            return "Работы уже готовятся в фоне: попробуй через несколько минут";

        var (files, error) = await DraftBuilder.BuildAsync(store, lesson, items, format, agent, cancellationToken, guides.Materials(lesson.Title, guideIds), extra(), store.AssignmentFiles(lesson.Title), remarks.ForPrepare(lesson.Title));
        if (error.Length > 0)
            return error;

        // Проверка не обязательна для готовности: если не вышла, её можно запустить из меню карточки
        if (files.Count > 0 && !cancellationToken.IsCancellationRequested)
            await CheckAsync(lesson, cancellationToken);

        return string.Empty;
    }

    /// <summary>
    /// Просит агента проверить готовые работы занятия: сверить с условием и методическими указаниями и найти факты без опоры. Работы при этом не меняются
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="cancellationToken">Отменяет запуск</param>
    /// <returns>Пустая строка, если отчёт записан, иначе причина</returns>
    public async Task<string> CheckAsync(Lesson lesson, CancellationToken cancellationToken)
    {
        try
        {
            var drafts = store.DraftFiles(lesson.Id);
            if (drafts.Count == 0)
                return "Готовой работы нет: проверять нечего";

            var folder = Path.Combine(store.DraftsDirectory(lesson.Id), ".check");
            var report = store.CheckReportPath(lesson.Id);
            Directory.CreateDirectory(folder);
            File.Delete(report);

            var readable = new List<string>();
            foreach (var draft in drafts)
            {
                var extension = Path.GetExtension(draft);
                var copy = Path.Combine(folder, Path.GetFileName(draft) + ".txt");
                if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
                    File.WriteAllText(copy, OfficeText.Docx(draft));
                else if (extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase))
                    File.WriteAllText(copy, OfficeText.Pptx(draft));

                readable.Add(File.Exists(copy) ? copy : draft);
            }

            var materials = guides.Materials(lesson.Title);
            var assignments = store.AssignmentFiles(lesson.Title);
            var prompt = DraftCheckPrompt.Build(lesson, readable, store.ReadItems(lesson.Id) ?? [], store.TranscriptPath(lesson.Id), assignments, materials, report);
            var error = await agent(prompt, store.Root, [.. materials.Directories, .. assignments.Select(path => Path.GetDirectoryName(path)!).Distinct()], cancellationToken);
            if (error.Length > 0)
                return error;

            return File.Exists(report) ? string.Empty : "Claude не записал отчёт";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or System.Xml.XmlException)
        {
            return "Не удалось подготовить проверку: " + exception.Message;
        }
    }

    /// <summary>
    /// Достаёт из вывода фоновой задачи готовые работы по строкам уведомления
    /// </summary>
    /// <param name="text">Вывод или сводка задачи; строки могут начинаться со времени</param>
    /// <returns>«Предмет, задание» без повторов в порядке появления</returns>
    public static IReadOnlyList<string> ReadyWorks(string? text) => text is null
        ? []
        : [.. text.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Contains(NOTICE, StringComparison.Ordinal))
            .Select(line => line[(line.IndexOf(NOTICE, StringComparison.Ordinal) + NOTICE.Length)..].Trim())
            .Where(work => work.Length > 0)
            .Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// Обрабатывает одно занятие: регистрирует образцы сданных работ и готовит несданные задания
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="doneWorks">Номера работ LMS, которые LMS показывает сданными</param>
    /// <param name="budget">Сколько работ ещё можно готовить в этом запуске</param>
    /// <param name="prepared">Куда добавить готовые работы</param>
    /// <param name="waiting">Куда добавить ждущие задания</param>
    /// <param name="failed">Куда добавить ошибки</param>
    /// <param name="cancellationToken">Отменяет запуск</param>
    /// <returns>Остаток лимита</returns>
    private async Task<int> RunLessonAsync(Lesson lesson, IReadOnlySet<string> doneWorks, int budget, List<string> prepared, List<string> waiting, List<string> failed, CancellationToken cancellationToken)
    {
        if (store.ReadItems(lesson.Id) is not { Count: > 0 } items)
            return budget;

        var archived = store.ArchivedNumbers(lesson.Id);
        var submitted = store.SubmittedNumbers(lesson.Id);

        /// <summary>
        /// Задание сдано: по файлу заданий, по отметке владельца или по LMS
        /// </summary>
        bool IsSubmitted(int number) => items[number - 1].Done || submitted.Contains(number) || (items[number - 1].ActivityId.Length > 0 && doneWorks.Contains(items[number - 1].ActivityId));

        for (var number = 1; number <= items.Count; number++)
        {
            if (!archived.Contains(number) && IsSubmitted(number))
                RegisterSample(lesson, number);
        }

        // Предмет и занятие, которые не готовятся («Не готовить», не «Расшифровывать»), агента не тратят
        if (!store.PreparesHomework(lesson) || store.SubjectFormatFor(lesson.Title) == HomeworkFormatEnum.Skip)
            return budget;

        var format = store.FormatFor(lesson.Id);
        var hasTranscript = store.HasTranscript(lesson.Id);
        var transcript = hasTranscript ? store.ReadTranscript(lesson.Id) : string.Empty;
        var confirmed = store.ConfirmedNumbers(lesson.Id);
        var produced = false;

        for (var number = 1; number <= items.Count; number++)
        {
            var item = items[number - 1];
            if (archived.Contains(number) || IsSubmitted(number) || store.SampleCandidate(lesson.Id, number) is not null)
                continue;

            var label = $"{lesson.Title}, {item.Title}";
            var assessment = ItemAssessor.Assess(item, EvidenceCheck.Verify(transcript, item.Evidence), hasTranscript, HasCondition(item), confirmed.Contains(number));
            if (assessment.Readiness == ItemReadinessEnum.NeedsConfirmation)
            {
                waiting.Add($"{label}: {assessment.Question}");

                continue;
            }

            var key = $"{lesson.Id}:{number}";
            var attempts = _ledger.Attempts(key);
            if (attempts >= MAX_ATTEMPTS)
            {
                waiting.Add($"{label}: агент не справился {MAX_ATTEMPTS} раза подряд, подготовь работу кнопкой");

                continue;
            }

            if (budget <= 0)
                continue;

            budget--;
            var error = await BuildOneAsync(lesson, items, number, format, key, attempts, cancellationToken);
            if (error.Length > 0)
            {
                failed.Add($"{label}: {error}");

                continue;
            }

            prepared.Add(label);
            produced = true;
        }

        if (produced)
        {
            var checkError = await CheckAsync(lesson, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (checkError.Length > 0)
                failed.Add($"{lesson.Title}: проверка не удалась: {checkError}");
        }

        return budget;
    }

    /// <summary>
    /// Готовит работу одного задания. Попытка засчитывается до запуска агента (так сбой процесса тоже считается), удача сбрасывает счётчик, отмена возвращает прежнее значение. Файл, который агент оставил при ошибке, удаляется, чтобы недописанная работа не стала «Готово»
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="items">Все задания занятия</param>
    /// <param name="number">Номер задания, с единицы</param>
    /// <param name="format">Формат работы</param>
    /// <param name="key">Ключ задания в учёте попыток</param>
    /// <param name="attempts">Сколько неудач подряд было до этой попытки</param>
    /// <param name="cancellationToken">Отменяет запуск</param>
    /// <returns>Пустая строка или причина неудачи</returns>
    /// <exception cref="OperationCanceledException">Запуск отменён</exception>
    private async Task<string> BuildOneAsync(Lesson lesson, IReadOnlyList<HomeworkItem> items, int number, HomeworkFormatEnum format, string key, int attempts, CancellationToken cancellationToken)
    {
        var folder = store.DraftsDirectory(lesson.Id);
        var before = TaskFiles(folder, number);
        _ledger.SetAttempts(key, attempts + 1);

        // Остальные задания отмечены сданными: агент выполняет только это, нумерация сохраняется
        var single = items.Select((item, index) => item with { Done = index != number - 1 }).ToArray();
        var (_, error) = await DraftBuilder.BuildAsync(store, lesson, single, format, agent, cancellationToken, guides.Materials(lesson.Title), string.Empty, store.AssignmentFiles(lesson.Title), remarks.ForPrepare(lesson.Title));
        if (cancellationToken.IsCancellationRequested)
        {
            RemoveNew(folder, number, before);
            _ledger.SetAttempts(key, attempts);
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (error.Length == 0 && store.SampleCandidate(lesson.Id, number) is not null)
        {
            _ledger.SetAttempts(key, 0);

            return string.Empty;
        }

        RemoveNew(folder, number, before);

        return error.Length > 0 ? error : "агент не записал файл";
    }

    /// <summary>
    /// Регистрирует работу сданного задания образцом предмета один раз. Если образец не записался (файл занят, недоступен), попробуем на следующем запуске
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="number">Номер задания, с единицы</param>
    private void RegisterSample(Lesson lesson, int number)
    {
        var key = $"{lesson.Id}:{number}";
        if (_ledger.SampleRegistered(key) || store.SampleCandidate(lesson.Id, number) is not { } file)
            return;

        try
        {
            guides.AddSample(lesson.Title, lesson.Id, number, file, fromUser: false);
            _ledger.MarkSample(key);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Список образцов занят или файл недоступен: повтор на следующем запуске
        }
    }

    /// <summary>
    /// Есть ли у задания условие из LMS: задание связано с работой LMS и её описание выгружено. Без этого задание ждёт подтверждения
    /// </summary>
    /// <param name="item">Задание</param>
    /// <returns>
    /// <see langword="true"/>, если описание работы лежит в <c>assignments/&lt;номер&gt;/assignment.md</c>
    /// </returns>
    private bool HasCondition(HomeworkItem item) =>
        item.ActivityId.Length > 0 && item.ActivityId.All(char.IsAsciiDigit) && File.Exists(Path.Combine(store.AssignmentDirectory(item.ActivityId), "assignment.md"));

    /// <summary>
    /// Все файлы задания в папке занятия, включая недописанные
    /// </summary>
    /// <param name="folder">Папка готовых файлов занятия</param>
    /// <param name="number">Номер задания</param>
    /// <returns>Пути к файлам</returns>
    private static HashSet<string> TaskFiles(string folder, int number) => Directory.Exists(folder)
        ? Directory.EnumerateFiles(folder, $"Задание {number}*").Where(file => IsTaskFile(file, number)).ToHashSet(StringComparer.OrdinalIgnoreCase)
        : [];

    /// <summary>
    /// Относится ли файл к заданию: «Задание N.*» или «Задание N. Название.*», но не «Задание N1.*»
    /// </summary>
    /// <param name="file">Путь к файлу</param>
    /// <param name="number">Номер задания</param>
    /// <returns>
    /// <see langword="true"/>, если файл этого задания
    /// </returns>
    private static bool IsTaskFile(string file, int number)
    {
        var name = Path.GetFileName(file);
        var prefix = $"Задание {number}";

        return name.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Удаляет файлы задания, которые появились во время неудачной попытки
    /// </summary>
    /// <param name="folder">Папка готовых файлов занятия</param>
    /// <param name="number">Номер задания</param>
    /// <param name="before">Файлы задания до попытки</param>
    private static void RemoveNew(string folder, int number, IReadOnlySet<string> before)
    {
        foreach (var file in TaskFiles(folder, number).Where(file => !before.Contains(file)))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Файл занят: он не будет считаться готовой работой, только если это недописанный файл; остальное покажет следующий запуск
            }
        }
    }

    /// <summary>
    /// Занимает подготовку: открывает файл блокировки без совместного доступа, он удаляется при закрытии. Работает между процессами и потоками
    /// </summary>
    /// <returns>Открытый файл или <see langword="null"/>, если подготовка уже идёт</returns>
    private FileStream? TryHold()
    {
        try
        {
            Directory.CreateDirectory(store.Root);

            return new FileStream(Path.Combine(store.Root, LOCK_FILE), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
