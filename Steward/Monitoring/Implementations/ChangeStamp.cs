using System.Globalization;
using System.IO;
using Steward.Status;

namespace Steward.Monitoring.Implementations;

/// <summary>
/// Хранит между запусками иконки момент последней правки, о которой уже показано уведомление
/// </summary>
public static class ChangeStamp
{
    /// <summary>
    /// Файл с моментом в круглом формате. Лежит в общей папке данных на диске репозитория, а не в профиле пользователя
    /// </summary>
    private static readonly string StampFile = Path.Combine(DataLocation.DataDirectory(), "last-notified.txt");

    /// <summary>
    /// Читает сохранённый момент. Если файла нет, начинает с текущего времени, чтобы старый журнал не всплыл уведомлениями
    /// </summary>
    /// <returns>
    /// Момент, после которого правки считаются новыми
    /// </returns>
    public static DateTimeOffset Load()
    {
        try
        {
            if (File.Exists(StampFile)
                && DateTimeOffset.TryParse(File.ReadAllText(StampFile).Trim(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var saved))
            {
                return saved;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        var now = DateTimeOffset.Now;
        Save(now);
        return now;
    }

    /// <summary>
    /// Записывает момент. Сбой записи не мешает работе иконки: уведомление тогда может повториться после перезапуска
    /// </summary>
    /// <param name="stamp">
    /// Момент самой свежей правки, о которой сказано
    /// </param>
    public static void Save(DateTimeOffset stamp)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StampFile)!);
            File.WriteAllText(StampFile, stamp.ToString("o", CultureInfo.InvariantCulture));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
