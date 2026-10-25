using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Steward.Schedule.Common.Models;
using Steward.Schedule.Scheduling.Models;

namespace Steward.Schedule.Scheduling.Implementations;

/// <summary>
/// Превращает сетку расписания в слоты одной колонки группы
/// </summary>
public static partial class ScheduleParser
{
    #region Parsing

    /// <summary>
    /// Читает все датированные временные слоты колонки <paramref name="groupColumn"/>
    /// </summary>
    /// <param name="csv">
    /// Экспорт листа вместе с заголовком
    /// </param>
    /// <param name="groupColumn">
    /// Заголовок копируемой колонки
    /// </param>
    /// <param name="timeZone">
    /// Пояс, в котором ставится время начала
    /// </param>
    /// <param name="today">
    /// Сегодняшняя дата в <paramref name="timeZone"/>, по ней выбирается год даты <c>DD.MM</c>
    /// </param>
    /// <returns>
    /// Слоты по внешнему идентификатору. Пустая ячейка — слот, у которого <see cref="ScheduleSlot.Lesson"/> равен <see langword="null"/>
    /// </returns>
    /// <exception cref="FormatException">
    /// Лист пуст, нет обязательной колонки, либо дата записана не как <c>DD.MM</c>
    /// </exception>
    public static IReadOnlyList<ScheduleSlot> Parse(string csv,
                                                    string groupColumn,
                                                    TimeZoneInfo timeZone,
                                                    DateOnly today)
    {
        var rows = CsvTableParser.Parse(csv);
        if (rows.Count == 0)
            throw new FormatException("The schedule CSV is empty.");

        var header = rows[0];
        var dateIndex = FindColumn(header, "Дата");
        var timeIndex = FindColumn(header, "время");
        var groupIndex = FindColumn(header, groupColumn);

        if (dateIndex < 0 || timeIndex < 0)
            throw new FormatException("The schedule CSV must have 'Дата' and 'время' columns.");

        if (groupIndex < 0)
            throw new FormatException($"Column '{groupColumn}' was not found in the schedule CSV.");

        var prefix = ExternalIdPrefix(groupColumn);
        var slots = new Dictionary<string, ScheduleSlot>(StringComparer.Ordinal);
        DateOnly? currentDate = null;

        for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var dateCell = Cell(row, dateIndex).Trim();
            if (dateCell.Length > 0)
                currentDate = ParseDate(dateCell, today, rowIndex + 1);

            if (currentDate is null)
                continue;

            var timeCell = Cell(row, timeIndex).Trim();
            if (!TryParseTimeRange(timeCell, out var startClock, out var duration))
                continue;

            var date = currentDate.Value;
            var externalId = $"{prefix}{date:yyyy-MM-dd}:{startClock.Hour:00}{startClock.Minute:00}";
            var note = NormalizeCell(Cell(row, groupIndex));
            if (note.Length == 0)
            {
                slots[externalId] = new ScheduleSlot(externalId, null);
                continue;
            }

            var title = BuildTitle(note);
            if (title.Length == 0)
            {
                slots[externalId] = new ScheduleSlot(externalId, null);
                continue;
            }

            note = BuildDescription(note);

            var localStart = new DateTime(date.Year,
                                          date.Month,
                                          date.Day,
                                          startClock.Hour,
                                          startClock.Minute,
                                          0,
                                          DateTimeKind.Unspecified);

            var start = new DateTimeOffset(localStart, timeZone.GetUtcOffset(localStart));
            slots[externalId] = new ScheduleSlot(externalId,
                new Lesson(externalId, title, note, start, duration));
        }

        return slots.Values
            .OrderBy(slot => slot.ExternalId, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Общий префикс внешних идентификаторов группы, включая завершающее двоеточие
    /// </summary>
    /// <param name="groupColumn">
    /// Заголовок колонки, из которого получается slug
    /// </param>
    /// <returns>
    /// Значение вроде <c>up:rki-2-26:</c>
    /// </returns>
    public static string ExternalIdPrefix(string groupColumn) => $"up:{Slug(groupColumn)}:";

    /// <summary>
    /// Ключ проекта группы. Начинается не с <c>up:</c>, чтобы его не приняли за ключ задачи занятия
    /// </summary>
    /// <param name="groupColumn">
    /// Заголовок колонки, из которого получается slug
    /// </param>
    /// <returns>
    /// Значение вроде <c>up-project:rki-2-26</c>
    /// </returns>
    public static string ProjectExternalId(string groupColumn) => $"up-project:{Slug(groupColumn)}";

    /// <summary>
    /// Превращает заголовок колонки в строчный slug, пригодный для внешнего идентификатора
    /// </summary>
    /// <param name="value">
    /// Заголовок колонки. Кириллица транслитерируется
    /// </param>
    /// <returns>
    /// Slug вроде <c>rki-2-26</c>
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// В заголовке нет букв и цифр
    /// </exception>
    public static string Slug(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(character);
                continue;
            }

            var latin = Transliterate(character);
            if (latin is not null)
            {
                builder.Append(latin);
                continue;
            }

            if (character is ' ' or '_' or '-' or '–' or '—')
                builder.Append('-');
        }

        var slug = RepeatedHyphen().Replace(builder.ToString(), "-").Trim('-');
        if (slug.Length == 0)
            throw new InvalidOperationException($"Cannot build an external id from group column '{value}'.");

        return slug;
    }

    #endregion Parsing

    #region Date and time

    /// <summary>
    /// Разбирает <c>DD.MM</c> и выбирает год по <paramref name="today"/>
    /// </summary>
    /// <param name="text">
    /// Ячейка с датой
    /// </param>
    /// <param name="today">
    /// Текущая дата в часовом поясе расписания
    /// </param>
    /// <param name="rowNumber">
    /// Номер строки с единицы, попадает в текст ошибки
    /// </param>
    /// <returns>
    /// Дата. Значение старше четырёх месяцев переносится на следующий год
    /// </returns>
    /// <exception cref="FormatException">
    /// В ячейке нет существующего дня и месяца
    /// </exception>
    private static DateOnly ParseDate(string text, DateOnly today, int rowNumber)
    {
        var match = DateCell().Match(text);
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var day)
            || !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var month))
        {
            throw new FormatException($"Row {rowNumber} has an invalid date '{text}'.");
        }

        try
        {
            var candidate = new DateOnly(today.Year, month, day);
            if (candidate < today.AddMonths(-4))
                candidate = candidate.AddYears(1);

            return candidate;
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new FormatException($"Row {rowNumber} has an invalid date '{text}'.");
        }
    }

    /// <summary>
    /// Разбирает диапазон вроде <c>17.20-18.40</c>
    /// </summary>
    /// <param name="text">
    /// Ячейка времени. Пустой текст и <c>выходной</c> диапазоном не считаются
    /// </param>
    /// <param name="start">
    /// Время начала, если метод вернул <see langword="true"/>
    /// </param>
    /// <param name="durationMinutes">
    /// Длительность в минутах, если метод вернул <see langword="true"/>
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если оба времени верны и длительность положительная
    /// </returns>
    private static bool TryParseTimeRange(string text, out TimeOnly start, out int durationMinutes)
    {
        start = default;
        durationMinutes = 0;
        if (text.Length == 0 || text.Equals("выходной", StringComparison.OrdinalIgnoreCase))
            return false;

        var match = TimeRange().Match(text);
        if (!match.Success)
            return false;

        if (!TryClock(match.Groups[1].Value, match.Groups[2].Value, out start)
            || !TryClock(match.Groups[3].Value, match.Groups[4].Value, out var end))
        {
            return false;
        }

        var startMinutes = start.Hour * 60 + start.Minute;
        var endMinutes = end.Hour * 60 + end.Minute;
        if (endMinutes <= startMinutes)
            endMinutes += 24 * 60;

        durationMinutes = endMinutes - startMinutes;
        return durationMinutes > 0;
    }

    /// <summary>
    /// Разбирает одну пару часов и минут
    /// </summary>
    /// <param name="hourText">
    /// Час от 0 до 23
    /// </param>
    /// <param name="minuteText">
    /// Минута от 0 до 59
    /// </param>
    /// <param name="time">
    /// Разобранное время, если метод вернул <see langword="true"/>
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если оба числа в допустимом диапазоне
    /// </returns>
    private static bool TryClock(string hourText, string minuteText, out TimeOnly time)
    {
        time = default;
        if (!int.TryParse(hourText, NumberStyles.None, CultureInfo.InvariantCulture, out var hour)
            || !int.TryParse(minuteText, NumberStyles.None, CultureInfo.InvariantCulture, out var minute)
            || hour is < 0 or > 23
            || minute is < 0 or > 59)
        {
            return false;
        }

        time = new TimeOnly(hour, minute);
        return true;
    }

    #endregion Date and time

    #region Titles

    /// <summary>
    /// Собирает название задачи из первой строки до запятой и добавляет тип занятия, если он стоит дальше
    /// </summary>
    /// <param name="note">
    /// Нормализованный текст ячейки
    /// </param>
    /// <returns>
    /// Название. Уже написанные в предмете <c>Лекция</c> или <c>Практика</c> не повторяются
    /// </returns>
    private static string BuildTitle(string note)
    {
        var firstLine = note.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
        var comma = firstLine.IndexOf(',');
        var head = (comma < 0 ? firstLine : firstLine[..comma]).Trim();
        var tail = comma < 0 ? note : note[(comma + 1)..];
        if (head.Length == 0)
            head = firstLine;

        var type = FindLessonType(tail);
        if (type is not null && FindLessonType(head) is null)
            head = $"{head} ({type})";

        return head;
    }

    /// <summary>
    /// Собирает описание: имя преподавателя, разделитель и адрес ячейки последней строкой
    /// </summary>
    /// <param name="note">
    /// Нормализованный текст ячейки. Адрес, если он есть, стоит последней строкой
    /// </param>
    /// <returns>
    /// Текст вида <c>Преподаватель: Имя</c>, затем <c>---</c> и адрес. Без адреса остаётся только строка преподавателя
    /// </returns>
    private static string BuildDescription(string note)
    {
        var lines = note.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        string? url = null;
        if (lines.Count > 0 && IsHttpAddress(lines[^1]))
        {
            url = lines[^1];
            lines.RemoveAt(lines.Count - 1);
        }

        var teacher = TeacherName(lines);
        if (teacher.Length == 0)
            return url ?? note;

        var description = $"Преподаватель: {teacher}";
        return url is null ? description : description + "\n---\n" + url;
    }

    /// <summary>
    /// Берёт имя преподавателя со следующей строки после предмета или после первой запятой
    /// </summary>
    /// <param name="lines">
    /// Строки ячейки без адреса
    /// </param>
    /// <returns>
    /// Имя без должности и без типа занятия. Пустая строка, если имени нет
    /// </returns>
    private static string TeacherName(List<string> lines)
    {
        if (lines.Count == 0)
            return "";

        var comma = lines[0].IndexOf(',');
        var tail = comma < 0 ? "" : lines[0][(comma + 1)..].Trim().Trim(',');
        var name = tail.Length > 0
            ? tail.Split(',')[0].Trim()
            : lines.Count > 1 ? lines[1].Trim().Trim(',') : "";

        name = ParentheticalLessonType().Replace(name, "").Trim().Trim(',');
        if (name.Length == 0 || name.Equals("преподаватель", StringComparison.OrdinalIgnoreCase))
            return "";

        return name;
    }

    /// <summary>
    /// Сообщает, что строка является адресом http или https
    /// </summary>
    /// <param name="line">
    /// Одна строка ячейки
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если строка начинается с <c>http://</c> или <c>https://</c> и не содержит пробелов
    /// </returns>
    private static bool IsHttpAddress(string line) =>
        (line.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        && !line.Contains(' ')
        && !line.Contains('\t');

    /// <summary>
    /// Находит первое слово типа занятия в <paramref name="text"/>
    /// </summary>
    /// <param name="text">
    /// Фрагмент ячейки
    /// </param>
    /// <returns>
    /// <c>Практика</c>, <c>Лекция</c> или <see langword="null"/>
    /// </returns>
    private static string? FindLessonType(string text)
    {
        var match = LessonTypePattern().Match(text);
        if (!match.Success)
            return null;

        return match.Value.Contains('п') || match.Value.Contains('П')
            ? "Практика"
            : "Лекция";
    }

    /// <summary>
    /// Обрезает каждую строку и выбрасывает пустые
    /// </summary>
    /// <param name="raw">
    /// Значение ячейки, как оно пришло из CSV
    /// </param>
    /// <returns>
    /// Нормализованный текст. Пустая ячейка или ячейка из пробелов становится пустой строкой
    /// </returns>
    private static string NormalizeCell(string raw)
    {
        var text = raw.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = text
            .Split('\n')
            .Select(line => Whitespace().Replace(line.Trim(), " "))
            .Where(line => line.Length > 0);
        return string.Join("\n", lines).Trim();
    }

    #endregion Titles

    #region Columns

    /// <summary>
    /// Возвращает ячейку или пустую строку, если строка короче <paramref name="index"/>
    /// </summary>
    /// <param name="row">
    /// Строка CSV
    /// </param>
    /// <param name="index">
    /// Индекс колонки с нуля
    /// </param>
    /// <returns>
    /// Текст ячейки
    /// </returns>
    private static string Cell(string[] row, int index) => index < row.Length ? row[index] : "";

    /// <summary>
    /// Ищет заголовок по обрезанному имени без учёта регистра
    /// </summary>
    /// <param name="header">
    /// Строка заголовков
    /// </param>
    /// <param name="name">
    /// Ожидаемый заголовок
    /// </param>
    /// <returns>
    /// Индекс с нуля или -1, если заголовка нет
    /// </returns>
    private static int FindColumn(string[] header, string name)
    {
        for (var i = 0; i < header.Length; i++)
        {
            if (header[i].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    #endregion Columns

    #region Transliteration

    /// <summary>
    /// Переводит одну строчную кириллическую букву в латиницу. Твёрдый и мягкий знаки дают пустую строку
    /// </summary>
    /// <param name="character">
    /// Уже приведённый к нижнему регистру символ
    /// </param>
    /// <returns>
    /// Латинский текст, пустая строка для знака или <see langword="null"/>, если символ не кириллический
    /// </returns>
    private static string? Transliterate(char character) => character switch
    {
        'а' => "a",
        'б' => "b",
        'в' => "v",
        'г' => "g",
        'д' => "d",
        'е' => "e",
        'ё' => "e",
        'ж' => "zh",
        'з' => "z",
        'и' => "i",
        'й' => "y",
        'к' => "k",
        'л' => "l",
        'м' => "m",
        'н' => "n",
        'о' => "o",
        'п' => "p",
        'р' => "r",
        'с' => "s",
        'т' => "t",
        'у' => "u",
        'ф' => "f",
        'х' => "h",
        'ц' => "ts",
        'ч' => "ch",
        'ш' => "sh",
        'щ' => "sch",
        'ъ' => "",
        'ы' => "y",
        'ь' => "",
        'э' => "e",
        'ю' => "yu",
        'я' => "ya",
        _ => null,
    };

    #endregion Transliteration

    #region Patterns

    /// <summary>
    /// Совпадает с днём и месяцем в виде <c>DD.MM</c>
    /// </summary>
    /// <returns>
    /// Шаблон даты
    /// </returns>
    [GeneratedRegex(@"^(\d{1,2})\.(\d{1,2})$")]
    private static partial Regex DateCell();

    /// <summary>
    /// Совпадает с временем начала и конца, разделёнными дефисом или тире
    /// </summary>
    /// <returns>
    /// Шаблон диапазона времени
    /// </returns>
    [GeneratedRegex(@"^(\d{1,2})[.:](\d{2})\s*[-–—]\s*(\d{1,2})[.:](\d{2})$")]
    private static partial Regex TimeRange();

    /// <summary>
    /// Совпадает со словами лекция и практика
    /// </summary>
    /// <returns>
    /// Шаблон типа занятия
    /// </returns>
    [GeneratedRegex(@"(?i)\b(лекция|практика)\b")]
    private static partial Regex LessonTypePattern();

    /// <summary>
    /// Тип занятия в скобках в конце имени, например <c>(Практика)</c>
    /// </summary>
    [GeneratedRegex(@"(?i)\s*\((лекция|практика)\)")]
    private static partial Regex ParentheticalLessonType();

    /// <summary>
    /// Совпадает с подряд идущими пробелами или табуляциями, которые схлопываются в один пробел
    /// </summary>
    /// <returns>
    /// Шаблон пробелов
    /// </returns>
    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex Whitespace();

    /// <summary>
    /// Совпадает с повторными дефисами, которые появляются при сборке slug
    /// </summary>
    /// <returns>
    /// Шаблон дефисов
    /// </returns>
    [GeneratedRegex("-{2,}")]
    private static partial Regex RepeatedHyphen();

    #endregion Patterns
}
