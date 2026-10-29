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
/// <param name="Cancelled">Запуск остановлен отменой или по сроку; в отчёте то, что успели до остановки</param>
public sealed record PrepareReport(IReadOnlyList<string> Prepared, IReadOnlyList<string> Waiting, IReadOnlyList<string> Failed, bool Busy = false, bool Cancelled = false)
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
public sealed partial class HomeworkPreparer(HomeworkStore store, GuideStore guides, RemarkStore remarks, DraftBuilder.AgentRun agent)
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
    /// Сколько раз пробовать удалить занятый файл прерванной попытки
    /// </summary>
    private const int DELETE_ATTEMPTS = 10;

    /// <summary>
    /// Пауза между попытками удаления, миллисекунды
    /// </summary>
    private const int DELETE_RETRY_MS = 200;

    /// <summary>
    /// Учёт попыток и зарегистрированных образцов
    /// </summary>
    private readonly PrepareLedger _ledger = new(store.Root);

    /// <summary>
    /// Готовит работы по найденным заданиям: от свежих занятий к старым, только несданные, не в архиве, без файла работы, у предметов, которые готовятся, и не больше лимита за запуск.
    /// Задание, в котором Steward не уверен, ждёт подтверждения. После ошибки агента повтор на следующем запуске, не больше трёх раз подряд. Каждая новая работа сразу проверяется. Работа сданного задания один раз становится образцом предмета.
    /// При отмене агент останавливается, прерванная попытка не засчитывается, а отчёт возвращается с тем, что успели
    /// </summary>
    /// <param name="maxWorks">Сколько работ готовить за запуск, с удачными и неудачными попытками</param>
    /// <param name="cancellationToken">Отменяет запуск</param>
    /// <param name="ready">Вызывается сразу после каждой готовой работы со строкой «предмет, задание», или <see langword="null"/></param>
    /// <returns>Готовые, ждущие и неудачные задания</returns>
    public async Task<PrepareReport> RunAsync(int maxWorks, CancellationToken cancellationToken, Action<string>? ready = null)
    {
        using var hold = TryHold();
        if (hold is null)
            return new PrepareReport([], [], [], Busy: true);

        var run = new RunState(Math.Max(maxWorks, 0), ready);
        try
        {
            var doneWorks = store.DoneWorkIds();
            foreach (var lesson in store.LoadLessons())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await RunLessonAsync(lesson, doneWorks, run, cancellationToken);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException or System.Text.Json.JsonException)
                {
                    run.Failed.Add(Flat($"{lesson.Title}: {exception.Message}"));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new PrepareReport(run.Prepared, run.Waiting, run.Failed, Cancelled: true);
        }

        return new PrepareReport(run.Prepared, run.Waiting, run.Failed);
    }

    /// <summary>
    /// Готовит работы, но не дольше срока: по его истечении агент останавливается, а отчёт возвращается с тем, что успели. Так шаг укладывается в таймаут фоновой задачи и запуск продолжается
    /// </summary>
    /// <param name="maxWorks">Сколько работ готовить за запуск</param>
    /// <param name="budget">Сколько времени дано шагу</param>
    /// <param name="ready">Вызывается сразу после каждой готовой работы, или <see langword="null"/></param>
    /// <param name="cancellationToken">Отменяет запуск раньше срока</param>
    /// <returns>Отчёт; <see cref="PrepareReport.Cancelled"/>, если срок вышел</returns>
    public async Task<PrepareReport> RunWithBudgetAsync(int maxWorks, TimeSpan budget, Action<string>? ready, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(budget);

        return await RunAsync(maxWorks, deadline.Token, ready);
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
    /// Достаёт готовые работы из строк уведомления: строка должна начинаться с «Готова работа: » (можно после метки времени журнала), упоминание в середине строки не считается
    /// </summary>
    /// <param name="text">Сводка или вывод задачи</param>
    /// <returns>«Предмет, задание» без повторов в порядке появления</returns>
    public static IReadOnlyList<string> ReadyWorks(string? text) => text is null
        ? []
        : [.. text.Split('\n')
            .Select(line => NoticeLine().Match(line.Trim()))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value.Trim())
            .Where(work => work.Length > 0)
            .Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// Строка уведомления: необязательная метка времени журнала «дд.мм.гггг чч:мм:сс», затем «Готова работа: » и работа
    /// </summary>
    /// <returns>Регулярное выражение</returns>
    [System.Text.RegularExpressions.GeneratedRegex(@"^(?:\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}:\d{2}\s+)?Готова работа: (.+)$")]
    private static partial System.Text.RegularExpressions.Regex NoticeLine();

    /// <summary>
    /// Сводит строку отчёта к одной строке: перевод строки в названии предмета или задания не может создать поддельную строку уведомления
    /// </summary>
    /// <param name="text">Строка отчёта</param>
    /// <returns>Одна строка</returns>
    private static string Flat(string text) => RemarkStore.Flatten(text);

    /// <summary>
    /// Обрабатывает одно занятие: регистрирует образцы сданных работ и готовит несданные задания
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="doneWorks">Номера работ LMS, которые LMS показывает сданными</param>
    /// <param name="run">Состояние запуска: остаток лимита и списки отчёта. Лимит, потраченный до ошибки, не возвращается</param>
    /// <param name="cancellationToken">Отменяет запуск</param>
    /// <returns>Задача обработки занятия</returns>
    private async Task RunLessonAsync(Lesson lesson, IReadOnlySet<string> doneWorks, RunState run, CancellationToken cancellationToken)
    {
        if (store.ReadItems(lesson.Id) is not { Count: > 0 } items)
            return;

        var archived = store.ArchivedNumbers(lesson.Id);
        var submitted = store.SubmittedAnywhere(lesson.Id, items, doneWorks);

        /// <summary>
        /// Задание сдано: по файлу заданий, по отметке владельца или по LMS
        /// </summary>
        bool IsSubmitted(int number) => submitted.Contains(number);

        for (var number = 1; number <= items.Count; number++)
        {
            if (!archived.Contains(number) && IsSubmitted(number))
                RegisterSample(lesson, number);
        }

        // Предмет и занятие, которые не готовятся («Не готовить», не «Расшифровывать»), агента не тратят
        if (!store.PreparesHomework(lesson) || store.SubjectFormatFor(lesson.Title) == HomeworkFormatEnum.Skip)
            return;

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

            var label = Flat($"{lesson.Title}, {item.Title}");
            var assessment = ItemAssessor.Assess(item, EvidenceCheck.Verify(transcript, item.Evidence), hasTranscript, HasCondition(item), confirmed.Contains(number));
            if (assessment.Readiness == ItemReadinessEnum.NeedsConfirmation)
            {
                run.Waiting.Add(Flat($"{label}: {assessment.Question}"));

                continue;
            }

            var key = $"{lesson.Id}:{number}";
            var attempts = _ledger.Attempts(key);
            if (attempts >= MAX_ATTEMPTS)
            {
                run.Waiting.Add($"{label}: агент не справился {MAX_ATTEMPTS} раза подряд, подготовь работу кнопкой");

                continue;
            }

            if (run.Budget <= 0)
                continue;

            run.Budget--;
            var error = await BuildOneAsync(lesson, items, number, format, key, attempts, cancellationToken);
            if (error.Length > 0)
            {
                run.Failed.Add(Flat($"{label}: {error}"));

                continue;
            }

            run.Prepared.Add(label);
            run.Ready?.Invoke(label);
            produced = true;
        }

        if (produced)
        {
            var checkError = await CheckAsync(lesson, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (checkError.Length > 0)
                run.Failed.Add(Flat($"{lesson.Title}: проверка не удалась: {checkError}"));
        }
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

        // Всё, что может упасть до агента, считается до засчёта попытки
        var materials = guides.Materials(lesson.Title);
        var assignments = store.AssignmentFiles(lesson.Title);
        var owner = remarks.ForPrepare(lesson.Title);

        // Остальные задания отмечены сданными: агент выполняет только это, нумерация сохраняется
        var single = items.Select((item, index) => item with { Done = index != number - 1 }).ToArray();
        _ledger.SetAttempts(key, attempts + 1);
        var (_, error) = await DraftBuilder.BuildAsync(store, lesson, single, format, agent, cancellationToken, materials, string.Empty, assignments, owner);
        if (cancellationToken.IsCancellationRequested)
        {
            await RemoveNewAsync(folder, number, before);
            TrySetAttempts(key, attempts);
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (error.Length == 0 && store.SampleCandidate(lesson.Id, number) is not null)
        {
            // Работа уже готова: сбой записи учёта её не отменяет, задание с файлом больше не готовится
            TrySetAttempts(key, 0);

            return string.Empty;
        }

        await RemoveNewAsync(folder, number, before);

        return error.Length > 0 ? error : "агент не записал файл";
    }

    /// <summary>
    /// Записывает число попыток, не роняя запуск, если файл учёта занят: прежнее значение тогда остаётся
    /// </summary>
    /// <param name="key">Ключ «занятие:номер»</param>
    /// <param name="count">Число попыток</param>
    private void TrySetAttempts(string key, int count)
    {
        try
        {
            _ledger.SetAttempts(key, count);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Учёт занят другим процессом: значение поправит следующий запуск
        }
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
        store.HasAssignmentCondition(item);

    /// <summary>
    /// Все файлы задания в папке занятия, включая недописанные
    /// </summary>
    /// <param name="folder">Папка готовых файлов занятия</param>
    /// <param name="number">Номер задания</param>
    /// <returns>Пути к файлам</returns>
    private static HashSet<string> TaskFiles(string folder, int number) => Directory.Exists(folder)
        ? Directory.EnumerateFiles(folder, $"Задание {number}*").Where(file => HomeworkStore.IsTaskFile(file, number)).ToHashSet(StringComparer.OrdinalIgnoreCase)
        : [];

    /// <summary>
    /// Удаляет файлы задания, которые появились во время неудачной или прерванной попытки. Занятый файл (его ещё держит останавливаемый агент) удаляется с несколькими повторами
    /// </summary>
    /// <param name="folder">Папка готовых файлов занятия</param>
    /// <param name="number">Номер задания</param>
    /// <param name="before">Файлы задания до попытки</param>
    /// <returns>Задача очистки</returns>
    private static async Task RemoveNewAsync(string folder, int number, IReadOnlySet<string> before)
    {
        foreach (var file in TaskFiles(folder, number).Where(file => !before.Contains(file)))
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    File.Delete(file);

                    break;
                }
                catch (Exception exception) when ((exception is IOException or UnauthorizedAccessException) && attempt < DELETE_ATTEMPTS)
                {
                    // Отмена уже наступила, поэтому пауза без токена
                    await Task.Delay(DELETE_RETRY_MS, CancellationToken.None);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // Файл так и не отпустили: следующий запуск увидит его готовой работой только если процесс дописал его целиком
                    break;
                }
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

    /// <summary>
    /// Состояние одного запуска: остаток лимита и списки отчёта. Общее для всех занятий, поэтому ошибка в занятии не возвращает потраченный лимит и не теряет готовые работы
    /// </summary>
    /// <param name="budget">Сколько работ можно готовить</param>
    /// <param name="ready">Сообщение о готовой работе или <see langword="null"/></param>
    private sealed class RunState(int budget, Action<string>? ready)
    {
        /// <summary>
        /// Сколько работ ещё можно готовить
        /// </summary>
        public int Budget { get; set; } = budget;

        /// <summary>
        /// Сообщение о готовой работе
        /// </summary>
        public Action<string>? Ready { get; } = ready;

        /// <summary>
        /// Готовые работы
        /// </summary>
        public List<string> Prepared { get; } = [];

        /// <summary>
        /// Задания, которые ждут владельца
        /// </summary>
        public List<string> Waiting { get; } = [];

        /// <summary>
        /// Ошибки
        /// </summary>
        public List<string> Failed { get; } = [];
    }
}
