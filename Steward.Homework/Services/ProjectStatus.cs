using System.Collections.Concurrent;
using System.Globalization;

namespace Steward.Homework.Services;

/// <summary>
/// Состояние единого проекта предмета, например проекта Unity, который растёт с каждым домашним заданием: сколько в нём скриптов и когда его последний раз меняли. Папка только читается
/// </summary>
/// <param name="Name">Имя проекта: папка, в которой лежит <c>Assets</c></param>
/// <param name="Exists">Папка проекта есть на диске</param>
/// <param name="Scripts">Сколько файлов <c>.cs</c> в <c>Assets</c></param>
/// <param name="LastChanged">Когда меняли самый свежий файл из <c>Assets</c>. <see langword="null"/>, если файлов нет</param>
public sealed record ProjectStatus(string Name, bool Exists, int Scripts, DateTime? LastChanged)
{
    /// <summary>
    /// Сколько секунд состояние держится в памяти: плитки обновляются часто, а обходить папку проекта каждый раз не нужно
    /// </summary>
    private const int CACHE_SECONDS = 60;

    /// <summary>
    /// Предел числа файлов, которые обходятся: огромная папка не должна задерживать окно
    /// </summary>
    private const int FILE_LIMIT = 50_000;

    /// <summary>
    /// Прочитанное состояние по папке проекта и время чтения
    /// </summary>
    private static readonly ConcurrentDictionary<string, (DateTime Read, ProjectStatus Status)> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Читает состояние проекта. Если в папке нет <c>Assets</c>, ищет его на уровень глубже: проект Unity часто лежит в папке с тем же именем внутри репозитория
    /// </summary>
    /// <param name="folder">Папка проекта или репозитория с проектом</param>
    /// <returns>Состояние проекта; без папки <see cref="Exists"/> ложно</returns>
    public static ProjectStatus Read(string folder)
    {
        if (Cache.TryGetValue(folder, out var cached) && (DateTime.UtcNow - cached.Read).TotalSeconds < CACHE_SECONDS)
            return cached.Status;

        var status = Scan(folder);
        Cache[folder] = (DateTime.UtcNow, status);

        return status;
    }

    /// <summary>
    /// Короткая строка для плитки: имя, число скриптов и день последней правки
    /// </summary>
    public string Text => !Exists
        ? $"Папка проекта не найдена: {Name}"
        : $"Проект {Name}: скриптов {Scripts}" + (LastChanged is { } changed ? $" · изменён {changed.ToString("dd.MM", CultureInfo.InvariantCulture)}" : string.Empty);

    /// <summary>
    /// Обходит папку проекта и считает скрипты и время последней правки
    /// </summary>
    /// <param name="folder">Папка проекта или репозитория</param>
    /// <returns>Состояние проекта</returns>
    private static ProjectStatus Scan(string folder)
    {
        var name = Path.GetFileName(folder.TrimEnd('\\', '/'));
        if (!Directory.Exists(folder))
            return new ProjectStatus(name, false, 0, null);

        var assets = Path.Combine(folder, "Assets");
        if (!Directory.Exists(assets))
        {
            var nested = Directory.EnumerateDirectories(folder).FirstOrDefault(child => Directory.Exists(Path.Combine(child, "Assets")));
            if (nested is null)
                return new ProjectStatus(name, true, 0, null);

            assets = Path.Combine(nested, "Assets");
            name = Path.GetFileName(nested);
        }

        var scripts = 0;
        DateTime? last = null;
        try
        {
            foreach (var file in Directory.EnumerateFiles(assets, "*", SearchOption.AllDirectories).Where(file => !file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)).Take(FILE_LIMIT))
            {
                if (file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    scripts++;

                var written = File.GetLastWriteTime(file);
                if (last is null || written > last)
                    last = written;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Папку в этот момент меняет Unity: берётся то, что успели прочитать
        }

        return new ProjectStatus(name, true, scripts, last);
    }
}
