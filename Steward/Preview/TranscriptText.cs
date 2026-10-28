namespace Steward.Preview;

/// <summary>
/// Делит текст расшифровки на шапку, резюме и полный текст по заголовкам «== Резюме ==» и «== Расшифровка ==»
/// </summary>
/// <param name="Header">Строки до резюме: занятие и дата</param>
/// <param name="Summary">Резюме встречи</param>
/// <param name="Body">Полный текст расшифровки</param>
public sealed record TranscriptText(string Header, string Summary, string Body)
{
    /// <summary>
    /// Разбирает файл расшифровки
    /// </summary>
    /// <param name="text">Содержимое файла</param>
    /// <returns>Части расшифровки. Если заголовков нет, весь текст считается полным текстом</returns>
    public static TranscriptText Parse(string text)
    {
        var normal = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var summaryAt = normal.IndexOf("== Резюме ==", StringComparison.Ordinal);
        var bodyAt = normal.IndexOf("== Расшифровка ==", StringComparison.Ordinal);
        if (summaryAt < 0 && bodyAt < 0)
            return new TranscriptText(string.Empty, string.Empty, normal.Trim());

        var header = summaryAt > 0 ? normal[..summaryAt].Trim() : bodyAt > 0 ? normal[..bodyAt].Trim() : string.Empty;
        var summary = summaryAt >= 0 ? normal[(summaryAt + "== Резюме ==".Length)..(bodyAt > summaryAt ? bodyAt : normal.Length)].Trim() : string.Empty;
        var body = bodyAt >= 0 ? normal[(bodyAt + "== Расшифровка ==".Length)..].Trim() : string.Empty;

        return new TranscriptText(header, summary, body);
    }
}
