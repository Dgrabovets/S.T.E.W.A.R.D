namespace Steward.Preview;

/// <summary>
/// Разбор файла расшифровки: тема встречи, краткое содержание и ключевые моменты из резюме. Полный текст по ролям не нужен и не показывается
/// </summary>
/// <param name="Topic">Тема встречи</param>
/// <param name="Overview">Краткое содержание: что было на занятии</param>
/// <param name="Points">Ключевые моменты: что пройдено и какие формулировки прозвучали</param>
public sealed record TranscriptText(string Topic, string Overview, IReadOnlyList<string> Points)
{
    /// <summary>
    /// Разбирает файл расшифровки по заголовкам «== Резюме ==» и «== Расшифровка ==»
    /// </summary>
    /// <param name="text">Содержимое файла</param>
    /// <returns>Части резюме. Пустые, если резюме в файле нет</returns>
    public static TranscriptText Parse(string text)
    {
        var normal = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var summaryAt = normal.IndexOf("== Резюме ==", StringComparison.Ordinal);
        if (summaryAt < 0)
            return new TranscriptText(string.Empty, string.Empty, []);

        var bodyAt = normal.IndexOf("== Расшифровка ==", summaryAt, StringComparison.Ordinal);
        var summary = normal[(summaryAt + "== Резюме ==".Length)..(bodyAt > 0 ? bodyAt : normal.Length)];

        var topic = string.Empty;
        var overview = new List<string>();
        var points = new List<string>();
        var section = string.Empty;
        foreach (var raw in summary.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw.StartsWith("Тема встречи:", StringComparison.Ordinal))
            {
                topic = raw["Тема встречи:".Length..].Trim();
                section = string.Empty;
            }
            else if (raw.StartsWith("Краткое содержание:", StringComparison.Ordinal))
            {
                section = "overview";
                var rest = raw["Краткое содержание:".Length..].Trim();
                if (rest.Length > 0)
                    overview.Add(rest);
            }
            else if (raw.StartsWith("Ключевые моменты:", StringComparison.Ordinal))
            {
                section = "points";
            }
            else if (section == "points")
            {
                points.Add(raw.TrimStart('•', '-', '*', ' ').Trim());
            }
            else if (section == "overview")
            {
                overview.Add(raw);
            }
        }

        return new TranscriptText(topic, string.Join("\n\n", overview), points);
    }
}
