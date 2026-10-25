namespace Steward.Status.Tests;

/// <summary>
/// Временная папка теста. Лежит рядом со сборкой тестов, то есть на том же диске, и удаляется после теста
/// </summary>
public sealed class TempDir : IDisposable
{
    /// <summary>
    /// Создаёт пустую папку
    /// </summary>
    public TempDir()
    {
        Path = System.IO.Path.Combine(AppContext.BaseDirectory, "tmp", Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(Path);
    }

    /// <summary>
    /// Полный путь к папке
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Папка статусов внутри
    /// </summary>
    public string StatusPath => System.IO.Path.Combine(Path, "status");

    /// <summary>
    /// Удаляет папку. Если процесс ещё держит файл, пробует ещё раз
    /// </summary>
    public void Dispose()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(Path, recursive: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
        }
    }
}
