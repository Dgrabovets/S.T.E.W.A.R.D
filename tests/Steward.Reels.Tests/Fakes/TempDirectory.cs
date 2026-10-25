namespace Steward.Reels.Tests.Fakes;

/// <summary>
/// Временная папка, которая удаляется вместе с объектом
/// </summary>
public sealed class TempDirectory : IDisposable
{
    /// <summary>
    /// Путь к папке
    /// </summary>
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "steward-reels-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Создаёт папку
    /// </summary>
    public TempDirectory() => Directory.CreateDirectory(Path);

    /// <summary>
    /// Удаляет папку со всем содержимым
    /// </summary>
    public void Dispose() => Directory.Delete(Path, recursive: true);
}