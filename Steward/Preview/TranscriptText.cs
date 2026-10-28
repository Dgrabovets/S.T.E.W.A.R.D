namespace Steward.Preview;

/// <summary>
/// Разбор файла расшифровки: тема, содержание, ключевые моменты, решения и задачи из резюме, а также статистика по репликам. Сам текст по ролям не показывается
/// </summary>
/// <param name="Topic">Тема встречи</param>
/// <param name="Overview">Краткое содержание: что было на занятии</param>
/// <param name="Points">Ключевые моменты: что пройдено</param>
/// <param name="Decisions">Принятые решения</param>
/// <param name="Tasks">Задачи, которые прозвучали на занятии</param>
/// <param name="Speakers">Кто сколько говорил: имя и число слов, от самого активного</param>
public sealed record TranscriptText(string Topic, string Overview, IReadOnlyList<string> Points, IReadOnlyList<string> Decisions, IReadOnlyList<string> Tasks, IReadOnlyList<(string Name, int Words)> Speakers)
{
    /// <summary>
    /// Пустой разбор
    /// </summary>
    public static TranscriptText Empty { get; } = new(string.Empty, string.Empty, [], [], [], []);

    /// <summary>
    /// Сколько слов сказано на занятии
    /// </summary>
    public int Words => Speakers.Sum(speaker => speaker.Words);

    /// <summary>
    /// Разбирает файл расшифровки по заголовкам «== Резюме ==» и «== Расшифровка ==»
    /// </summary>
    /// <param name="text">Содержимое файла</param>
    /// <returns>Части резюме и статистика реплик. Пустой разбор, если в файле нет ни резюме, ни реплик</returns>
    public static TranscriptText Parse(string text)
    {
        var normal = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var summaryAt = normal.IndexOf("== Резюме ==", StringComparison.Ordinal);
        var bodyAt = normal.IndexOf("== Расшифровка ==", StringComparison.Ordinal);
        var summary = summaryAt >= 0 ? normal[(summaryAt + "== Резюме ==".Length)..(bodyAt > summaryAt ? bodyAt : normal.Length)] : string.Empty;

        var topic = string.Empty;
        var overview = new List<string>();
        var lists = new Dictionary<string, List<string>>(StringComparer.Ordinal) { ["points"] = [], ["decisions"] = [], ["tasks"] = [] };
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
            else if (raw.StartsWith("Принятые решения:", StringComparison.Ordinal))
            {
                section = "decisions";
            }
            else if (raw.StartsWith("Задачи:", StringComparison.Ordinal))
            {
                section = "tasks";
            }
            else if (lists.TryGetValue(section, out var list))
            {
                list.Add(raw.TrimStart('•', '-', '*', ' ').Trim());
            }
            else if (section == "overview")
            {
                overview.Add(raw);
            }
        }

        var words = new Dictionary<string, int>(StringComparer.Ordinal);
        if (bodyAt >= 0)
        {
            foreach (var line in normal[(bodyAt + "== Расшифровка ==".Length)..].Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                // Реплика выглядит так: «[00:03:36] Имя Фамилия: текст»
                var close = line.IndexOf(']', StringComparison.Ordinal);
                var colon = close > 0 ? line.IndexOf(':', close) : -1;
                if (line.StartsWith('[') && colon > close)
                {
                    var name = line[(close + 1)..colon].Trim();
                    var count = line[(colon + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                    words[name] = words.GetValueOrDefault(name) + count;
                }
            }
        }

        return new TranscriptText(topic, string.Join("\n\n", overview), lists["points"], lists["decisions"], lists["tasks"], [.. words.OrderByDescending(pair => pair.Value).Select(pair => (pair.Key, pair.Value))]);
    }
}
