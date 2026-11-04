using System.Text.Encodings.Web;
using System.Text.Json;
using Steward.Mail.Interfaces;
using Steward.Mail.Models;

namespace Steward.Mail.Services;

/// <summary>
/// Архив важных писем в одном JSON-файле: новые письма в начале, хранится не больше <see cref="LIMIT"/> писем
/// </summary>
public sealed class MailArchive : IMailArchive
{
    /// <summary>
    /// Сколько писем хранит архив: старые выпадают
    /// </summary>
    private const int LIMIT = 500;

    /// <summary>
    /// Пишет JSON в camelCase с отступами, кириллица остаётся буквами
    /// </summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Файл архива
    /// </summary>
    private readonly string _path;

    /// <summary>
    /// Создаёт архив
    /// </summary>
    /// <param name="path">Файл архива, например <c>data/mail/archive.json</c></param>
    public MailArchive(string path) => _path = path;

    /// <inheritdoc />
    public void Add(ArchivedLetter letter)
    {
        var letters = Load();
        if (letters.Any(item => string.Equals(item.MessageId, letter.MessageId, StringComparison.Ordinal)))
            return;

        letters.Insert(0, letter);
        if (letters.Count > LIMIT)
            letters.RemoveRange(LIMIT, letters.Count - LIMIT);

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = $"{_path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(letters, Json));
        File.Move(temp, _path, overwrite: true);
    }

    /// <summary>
    /// Читает архив; повреждённый файл не мешает проверке почты, архив начинается заново
    /// </summary>
    /// <returns>Письма от новых к старым</returns>
    private List<ArchivedLetter> Load()
    {
        try
        {
            return File.Exists(_path) ? JsonSerializer.Deserialize<List<ArchivedLetter>>(File.ReadAllText(_path), Json) ?? [] : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }
}
