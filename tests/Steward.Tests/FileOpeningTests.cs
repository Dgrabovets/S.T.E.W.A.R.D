using Steward.Hosting;

namespace Steward.Tests;

/// <summary>
/// Файл, который написал агент, нельзя запускать: документы открываются оболочкой, код в Блокноте, остальное показывается в проводнике
/// </summary>
public class FileOpeningTests
{
    /// <summary>
    /// Документ открывает оболочка, код идёт в Блокнот, а исполняемые и скриптовые файлы только показываются в проводнике
    /// </summary>
    [Theory]
    [InlineData(@"C:\work\Задание 1.docx", true, @"C:\work\Задание 1.docx")]
    [InlineData(@"C:\work\Задание 1.py", false, "notepad.exe")]
    [InlineData(@"C:\work\Задание 1.bat", false, "explorer.exe")]
    [InlineData(@"C:\work\Задание 1.LNK", false, "explorer.exe")]
    [InlineData(@"C:\work\Задание 1.cmd", false, "explorer.exe")]
    [InlineData(@"C:\work\Задание 1.doc", false, "explorer.exe")]
    public void Only_documents_go_to_the_shell(string path, bool shell, string program)
    {
        var start = FileOpening.Plan(path);

        Assert.Equal(shell, start.UseShellExecute);
        Assert.Equal(program, start.FileName);
    }

    /// <summary>
    /// Путь с пробелами идёт в кавычках: проводник получает «/select,"путь"», а Блокнот путь в кавычках
    /// </summary>
    [Fact]
    public void A_path_with_spaces_is_quoted_around_the_path_only()
    {
        Assert.Equal(@"/select,""C:\my work\Задание 1.bat""", FileOpening.Plan(@"C:\my work\Задание 1.bat").Arguments);
        Assert.Equal(@"""C:\my work\Задание 1.py""", FileOpening.Plan(@"C:\my work\Задание 1.py").Arguments);
    }
}
