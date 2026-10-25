using Steward.Reels.Services;
using Steward.Reels.Tests.Fakes;

namespace Steward.Reels.Tests;

/// <summary>
/// Проверяет, что бот помнит, до какого сообщения дочитал
/// </summary>
public class OffsetStoreTests
{
    /// <summary>
    /// Пока ничего не записано, чтение идёт с нуля
    /// </summary>
    [Fact]
    public void Read_starts_from_zero()
    {
        using var directory = new TempDirectory();

        Assert.Equal(0, new OffsetStore(Path.Combine(directory.Path, "data", "offset.txt")).Read());
    }

    /// <summary>
    /// Записанное число читается обратно, испорченный файл даёт ноль
    /// </summary>
    [Fact]
    public void Write_round_trips_and_a_broken_file_reads_as_zero()
    {
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "data", "offset.txt");
        var store = new OffsetStore(path);

        store.Write(42);
        Assert.Equal(42, store.Read());

        File.WriteAllText(path, "мусор");
        Assert.Equal(0, store.Read());
    }
}