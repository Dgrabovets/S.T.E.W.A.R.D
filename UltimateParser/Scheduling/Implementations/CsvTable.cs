using System.Text;

namespace UltimateParser.Scheduling.Implementations;

/// <summary>
/// Разбирает CSV по RFC 4180, включая кавычки, запятые и переносы строк внутри ячейки
/// </summary>
public static class CsvTable
{
    /// <summary>
    /// Делит текст CSV на строки полей
    /// </summary>
    /// <param name="text">
    /// Весь документ CSV. Начальная метка порядка байтов отбрасывается
    /// </param>
    /// <returns>
    /// Строки в порядке файла. Завершающий перевод строки не добавляет пустую строку, если в ней нет поля
    /// </returns>
    public static List<string[]> Parse(string text)
    {
        if (text.StartsWith('\uFEFF'))
            text = text[1..];

        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var current = text[i];
            if (inQuotes)
            {
                if (current == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(current);
                }

                continue;
            }

            switch (current)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row.ToArray());
                    row.Clear();
                    break;
                case '\r':
                    break;
                default:
                    field.Append(current);
                    break;
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row.ToArray());
        }

        return rows;
    }
}
