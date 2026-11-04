using System.Diagnostics;
using System.IO;

namespace Steward.Hosting;

/// <summary>
/// Решает, как безопасно открыть файл по кнопке в окне. Готовые работы пишет агент по тексту расшифровок и LMS, то есть по чужим данным, поэтому файл с произвольным расширением нельзя отдавать оболочке Windows: она его запустит
/// </summary>
public static class FileOpening
{
    /// <summary>
    /// Документы и картинки: их открывает программа, которую Windows связала с типом, и они ничего не запускают. Старые форматы .doc, .xls и .ppt сюда не входят: в них бывают макросы
    /// </summary>
    private static readonly HashSet<string> Documents = [".docx", ".pptx", ".xlsx", ".pdf", ".md", ".txt", ".csv", ".png", ".jpg", ".jpeg"];

    /// <summary>
    /// Текст кода и данных: показывается в Блокноте, который ничего не выполняет, даже если Windows связала тип с интерпретатором
    /// </summary>
    private static readonly HashSet<string> Code = [".py", ".cs", ".js", ".ts", ".java", ".cpp", ".c", ".h", ".json", ".xml", ".ipynb", ".sql", ".yml", ".yaml", ".log"];

    /// <summary>
    /// Собирает запуск для пути
    /// </summary>
    /// <param name="path">Путь к файлу или папке</param>
    /// <returns>Папка и документ открываются оболочкой, код в Блокноте, всё остальное показывается в проводнике и не запускается</returns>
    public static ProcessStartInfo Plan(string path)
    {
        // ErrorDialog снимает с вызова флаг «без окон»: без него .NET не даёт Windows показать выбор программы для файла без связи и сразу бросает исключение
        if (Directory.Exists(path) || Documents.Contains(Path.GetExtension(path).ToLowerInvariant()))
            return new ProcessStartInfo(path) { UseShellExecute = true, ErrorDialog = true };

        // Кавычки нужны вокруг пути, а не вокруг всего ключа: ArgumentList обернул бы «/select,путь» целиком, и проводник не нашёл бы файл с пробелом в пути
        return Code.Contains(Path.GetExtension(path).ToLowerInvariant())
            ? new ProcessStartInfo("notepad.exe") { UseShellExecute = false, Arguments = $"\"{path}\"" }
            : new ProcessStartInfo("explorer.exe") { UseShellExecute = false, Arguments = $"/select,\"{path}\"" };
    }
}
