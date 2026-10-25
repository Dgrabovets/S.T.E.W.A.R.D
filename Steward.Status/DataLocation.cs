namespace Steward.Status;

/// <summary>
/// Находит папку с данными и файл задач, чтобы все процессы и приложение в трее смотрели в одно место
/// </summary>
public static class DataLocation
{
    /// <summary>
    /// Переменная окружения с папкой данных. Раннер передаёт её каждому запущенному процессу
    /// </summary>
    public const string DATA_ENVIRONMENT_VARIABLE = "STEWARD_DATA";

    /// <summary>
    /// Переменная окружения с путём к файлу задач
    /// </summary>
    public const string JOBS_ENVIRONMENT_VARIABLE = "STEWARD_JOBS";

    /// <summary>
    /// Файл решения, по которому узнаётся корень репозитория
    /// </summary>
    private const string SOLUTION_FILE = "Steward.slnx";

    /// <summary>
    /// Поднимается от каталога и ищет корень репозитория
    /// </summary>
    /// <param name="startDirectory">Откуда начинать. По умолчанию каталог программы</param>
    /// <returns>
    /// Корень репозитория или <see langword="null"/>, если программа лежит вне репозитория
    /// </returns>
    public static string? FindRepoRoot(string? startDirectory = null)
    {
        var directory = new DirectoryInfo(startDirectory ?? AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SOLUTION_FILE)))
                return directory.FullName;

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// Папка данных: явный путь, затем переменная окружения, затем <c>data</c> в корне репозитория, затем рядом с программой
    /// </summary>
    /// <param name="explicitDirectory">
    /// Путь из настроек или <see langword="null"/>
    /// </param>
    /// <param name="startDirectory">Откуда искать корень репозитория. По умолчанию каталог программы</param>
    /// <returns>Путь к папке данных. Папка создаётся при первой записи</returns>
    public static string DataDirectory(string? explicitDirectory = null, string? startDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitDirectory))
            return explicitDirectory;

        var fromEnvironment = Environment.GetEnvironmentVariable(DATA_ENVIRONMENT_VARIABLE);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment;

        var root = FindRepoRoot(startDirectory);
        return Path.Combine(root ?? startDirectory ?? AppContext.BaseDirectory, "data");
    }

    /// <summary>
    /// Папка файлов статусов внутри папки данных
    /// </summary>
    /// <param name="explicitDirectory">
    /// Папка данных из настроек или <see langword="null"/>
    /// </param>
    /// <param name="startDirectory">Откуда искать корень репозитория</param>
    /// <returns>
    /// Путь к папке <c>status</c>
    /// </returns>
    public static string StatusDirectory(string? explicitDirectory = null, string? startDirectory = null) =>
        Path.Combine(DataDirectory(explicitDirectory, startDirectory), "status");

    /// <summary>
    /// Файл со списком задач: переменная окружения, затем <c>config/jobs.json</c> в корне репозитория
    /// </summary>
    /// <param name="startDirectory">Откуда искать корень репозитория</param>
    /// <returns>Путь к файлу. Файла может не быть: тогда задач нет</returns>
    public static string JobsFile(string? startDirectory = null)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(JOBS_ENVIRONMENT_VARIABLE);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment;

        var root = FindRepoRoot(startDirectory);
        return Path.Combine(root ?? startDirectory ?? AppContext.BaseDirectory, "config", "jobs.json");
    }
}
