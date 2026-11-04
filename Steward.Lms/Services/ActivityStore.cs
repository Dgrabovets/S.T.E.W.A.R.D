using System.Text.Encodings.Web;
using System.Text.Json;
using Steward.Lms.Models;

namespace Steward.Lms.Services;

/// <summary>
/// Хранит последний снимок работ из LMS в <c>data/homework/activities.json</c>: по нему приложение сможет показать, что сдано, не обращаясь к сайту
/// </summary>
public static class ActivityStore
{
    /// <summary>
    /// Пишет JSON в camelCase с отступами, кириллица остаётся буквами
    /// </summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Записывает снимок через временный файл
    /// </summary>
    /// <param name="directory">Папка домашних заданий</param>
    /// <param name="activities">Работы из LMS</param>
    public static void Save(string directory, IReadOnlyList<LmsActivity> activities)
    {
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, "activities.json");
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(activities, Json));
        File.Move(temp, path, overwrite: true);
    }
}
