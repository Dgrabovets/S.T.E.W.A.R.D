using System.Globalization;

namespace Steward.Reels.Services;

/// <summary>
/// Помнит номер следующего обновления Telegram, чтобы одно сообщение не читалось дважды
/// </summary>
/// <param name="path">Файл, в котором лежит число</param>
public sealed class OffsetStore(string path)
{
    /// <summary>
    /// Читает сохранённый номер
    /// </summary>
    /// <returns>Номер или ноль, если файла нет или он испорчен</returns>
    public long Read() =>
        File.Exists(path) && long.TryParse(File.ReadAllText(path).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;

    /// <summary>
    /// Записывает номер через временный файл
    /// </summary>
    /// <param name="offset">Номер следующего обновления</param>
    public void Write(long offset)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var temp = path + ".tmp";
        File.WriteAllText(temp, offset.ToString(CultureInfo.InvariantCulture));
        File.Move(temp, path, overwrite: true);
    }
}