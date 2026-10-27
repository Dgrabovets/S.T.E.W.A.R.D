using System.Globalization;
using System.Text.Json;
using Steward.Homework.Models;
using Steward.Homework.Storage;
using Steward.Lms.Common;
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
                    summary += "\n" + await FetchAsync(browser, store);
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
    /// Забирает расшифровки занятий с отметкой «забирать», у которых текста ещё нет, и кладёт их в <c>transcripts</c>
    /// </summary>
    /// <param name="browser">Браузер MTS Link</param>
    /// <param name="store">Папка домашних заданий</param>
    /// <returns>Строка итога для статуса</returns>
    private static async Task<string> FetchAsync(MtsBrowser browser, HomeworkStore store)
    {
        var states = store.LoadStates();
        var wanted = store.LoadLessons()
            .Where(lesson => lesson.RecordUrl.Length > 0 && states.TryGetValue(lesson.Id, out var state) && state == LessonStateEnum.Fetch && !store.HasTranscript(lesson.Id))
            .ToDictionary(lesson => lesson.Id, StringComparer.Ordinal);
        if (wanted.Count == 0)
            return "Расшифровки: новых к выгрузке нет";

        var received = await browser.FetchAsync(
            wanted.ToDictionary(pair => pair.Key, pair => pair.Value.RecordUrl, StringComparer.Ordinal),
            (lessonId, transcript) =>
            {
                var path = store.TranscriptPath(lessonId);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, TranscriptFormatter.Format(wanted[lessonId], transcript));
                Log($"Расшифровка: {wanted[lessonId].Title}, {wanted[lessonId].Date:dd.MM.yyyy}");

                return Task.CompletedTask;
            },
            CancellationToken.None);

        return $"Расшифровки: получено {received} из {wanted.Count}";
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
