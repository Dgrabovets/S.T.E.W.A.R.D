using System.Globalization;
using System.Text.Json;

namespace Steward.Status;

/// <summary>
/// Результат чтения списка задач
/// </summary>
/// <param name="Jobs">
/// Задачи, прошедшие проверку
/// </param>
/// <param name="Errors">
/// Что не удалось прочитать и почему. Пусто, если всё в порядке
/// </param>
public sealed record JobRegistryResult(IReadOnlyList<JobDefinition> Jobs, IReadOnlyList<string> Errors);

/// <summary>
/// Читает файл <c>jobs.json</c> со списком задач
/// </summary>
public static class JobRegistry
{
    /// <summary>
    /// Токен корня репозитория в путях и аргументах
    /// </summary>
    private const string REPO_TOKEN = "{repo}";

    /// <summary>
    /// Файл можно писать руками: комментарии и лишние запятые разрешены
    /// </summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    #region Loading

    /// <summary>
    /// Читает файл. Если файла нет, задач просто нет
    /// </summary>
    /// <param name="path">
    /// Путь к <c>jobs.json</c>
    /// </param>
    /// <param name="repoRoot">
    /// Корень репозитория для подстановки <c>{repo}</c> или <see langword="null"/>
    /// </param>
    /// <returns>
    /// Задачи и ошибки чтения
    /// </returns>
    public static JobRegistryResult Load(string path, string? repoRoot)
    {
        if (!File.Exists(path))
            return new JobRegistryResult([], []);

        try
        {
            return Parse(File.ReadAllText(path), repoRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new JobRegistryResult([], [$"Не удалось прочитать {path}: {exception.Message}"]);
        }
    }

    /// <summary>
    /// Разбирает текст файла: плохая задача пропускается, остальные работают
    /// </summary>
    /// <param name="json">
    /// Содержимое <c>jobs.json</c>
    /// </param>
    /// <param name="repoRoot">
    /// Корень репозитория для подстановки <c>{repo}</c> или <see langword="null"/>
    /// </param>
    /// <returns>
    /// Задачи и ошибки разбора
    /// </returns>
    public static JobRegistryResult Parse(string json, string? repoRoot)
    {
        JobFile? file;
        try
        {
            file = JsonSerializer.Deserialize<JobFile>(json, Json);
        }
        catch (JsonException exception)
        {
            return new JobRegistryResult([], [$"jobs.json не читается: {exception.Message}"]);
        }

        var jobs = new List<JobDefinition>();
        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in file?.Jobs ?? [])
        {
            var job = Expand(raw, repoRoot);
            var problem = Validate(job, repoRoot is not null || !Uses(raw));
            if (problem is null && !seen.Add(job.Id))
                problem = "идентификатор уже занят";

            if (problem is not null)
            {
                errors.Add($"Задача «{(string.IsNullOrWhiteSpace(raw.Id) ? "без id" : raw.Id)}»: {problem}");
                continue;
            }

            jobs.Add(job);
        }

        return new JobRegistryResult(jobs, errors);
    }

    #endregion Loading

    #region Checks

    /// <summary>
    /// Проверяет задачу
    /// </summary>
    /// <param name="job">
    /// Задача после подстановки
    /// </param>
    /// <param name="repoKnown">
    /// <see langword="false"/>, если задача просит <c>{repo}</c>, а корень репозитория неизвестен
    /// </param>
    /// <returns>
    /// Что не так или <see langword="null"/>
    /// </returns>
    private static string? Validate(JobDefinition job, bool repoKnown)
    {
        if (!StatusStore.IsValidId(job.Id))
            return "нужен id из латиницы, цифр, дефиса и подчёркивания";
        if (string.IsNullOrWhiteSpace(job.Command))
            return "не указана команда";
        if (!repoKnown)
            return "путь содержит {repo}, а корень репозитория не найден";
        if (job.TimeoutMinutes <= 0)
            return "таймаут должен быть больше нуля";

        foreach (var trigger in job.Triggers)
        {
            var problem = trigger.Type switch
            {
                JobTrigger.START => null,
                JobTrigger.DAILY => TimeOnly.TryParseExact(trigger.At, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ? null : "для daily нужно время в формате HH:mm",
                JobTrigger.INTERVAL => trigger.EveryMinutes is >= 1 ? null : "для interval нужно число минут от 1",
                _ => $"неизвестное условие «{trigger.Type}»",
            };
            if (problem is not null)
                return problem;
        }

        return null;
    }

    /// <summary>
    /// Подставляет корень репозитория вместо <c>{repo}</c>
    /// </summary>
    /// <param name="job">
    /// Задача из файла
    /// </param>
    /// <param name="repoRoot">
    /// Корень репозитория или <see langword="null"/>
    /// </param>
    /// <returns>
    /// Задача с готовыми путями
    /// </returns>
    private static JobDefinition Expand(JobDefinition job, string? repoRoot)
    {
        if (repoRoot is null)
            return job;

        return job with
        {
            Command = job.Command.Replace(REPO_TOKEN, repoRoot, StringComparison.Ordinal),
            Arguments = job.Arguments.Replace(REPO_TOKEN, repoRoot, StringComparison.Ordinal),
            WorkingDirectory = job.WorkingDirectory?.Replace(REPO_TOKEN, repoRoot, StringComparison.Ordinal),
        };
    }

    /// <summary>
    /// Сообщает, что задача использует <c>{repo}</c>
    /// </summary>
    /// <param name="job">
    /// Задача из файла
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если токен есть в команде, аргументах или каталоге
    /// </returns>
    private static bool Uses(JobDefinition job) =>
        job.Command.Contains(REPO_TOKEN, StringComparison.Ordinal)
        || job.Arguments.Contains(REPO_TOKEN, StringComparison.Ordinal)
        || (job.WorkingDirectory?.Contains(REPO_TOKEN, StringComparison.Ordinal) ?? false);

    #endregion Checks

    /// <summary>
    /// Корень файла: список задач
    /// </summary>
    /// <param name="Jobs">
    /// Задачи как они записаны
    /// </param>
    private sealed record JobFile(List<JobDefinition>? Jobs);
}
