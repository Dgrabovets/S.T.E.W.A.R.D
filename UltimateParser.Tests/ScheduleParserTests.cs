using UltimateParser.Common.Models;
using UltimateParser.Scheduling.Implementations;

namespace UltimateParser.Tests;

/// <summary>
/// Проверяет, что сетка расписания превращается в ожидаемые слоты
/// </summary>
public class ScheduleParserTests
{
    /// <summary>
    /// Московский часовой пояс, которым проставляется смещение начала
    /// </summary>
    private static readonly TimeZoneInfo Moscow = SyncOptions.ResolveTimeZone("Europe/Moscow");

    /// <summary>
    /// Фрагмент реальной октябрьской сетки со слитыми датами и многострочной ячейкой
    /// </summary>
    private const string Grid = """
        ,Дата,время,АДИИ-1-26,АДИИ-2-26,АДИИ-3-26,РКИ-1-26,РКИ-2-26,
        чт,01.10,17.20-18.40,,,,,,
        ,,18.50-20.10,,,,,,
        ,,20.10-21.30,,,,,"Иностранный язык делового и профессионального общения, Будник Екатерина Александровна, канд. пед. наук, доцент",
        пт,02.10,17.20-18.40,,,,,,
        сб,03.10,выходной,,,,,,
        вс,04.10,,,,,,,
        пн,05.10,20.10-21.30,,,,,"Философские проблемы науки и техники, Каптерев Андрей Игоревич (Практика)
        д-р социол. наук, д-р пед. наук, профессор",
        вт,06.10,17.20-18.40,,,,,"Индивидуальный творческий проект (Практика), Ромашкова Оксана Николаевна, д-р техн. наук профессор",
        """;

    #region Facts

    /// <summary>
    /// Ячейки дат действуют на следующие строки, выходные пропускаются, а в названии сохраняется тип занятия
    /// </summary>
    [Fact]
    public void Parse_reads_group_column_and_carries_the_date_forward()
    {
        var slots = ScheduleParser.Parse(Grid, "РКИ-2-26", Moscow, new DateOnly(2026, 10, 8));
        var lessons = slots.Where(slot => slot.Lesson is not null).Select(slot => slot.Lesson!).ToArray();

        Assert.Equal(3, lessons.Length);
        Assert.Contains(slots, slot => slot.ExternalId == "up:rki-2-26:2026-10-01:1720" && slot.Lesson is null);
        Assert.Contains(slots, slot => slot.ExternalId == "up:rki-2-26:2026-10-01:1850" && slot.Lesson is null);
        Assert.DoesNotContain(slots, slot => slot.ExternalId.Contains("2026-10-03", StringComparison.Ordinal));
        Assert.DoesNotContain(slots, slot => slot.ExternalId.Contains("2026-10-04", StringComparison.Ordinal));

        var language = lessons.Single(lesson => lesson.ExternalId == "up:rki-2-26:2026-10-01:2010");
        Assert.Equal("Иностранный язык делового и профессионального общения", language.Title);
        Assert.Equal(80, language.TimeLengthMinutes);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 20, 10, 0, TimeSpan.FromHours(3)), language.Start);
        Assert.Contains("Будник Екатерина Александровна", language.Note, StringComparison.Ordinal);

        var practice = lessons.Single(lesson => lesson.ExternalId == "up:rki-2-26:2026-10-05:2010");
        Assert.Equal("Философские проблемы науки и техники (Практика)", practice.Title);
        Assert.Contains("д-р социол. наук", practice.Note, StringComparison.Ordinal);

        var project = lessons.Single(lesson => lesson.ExternalId == "up:rki-2-26:2026-10-06:1720");
        Assert.Equal("Индивидуальный творческий проект (Практика)", project.Title);
        Assert.Equal(80, project.TimeLengthMinutes);
    }

    /// <summary>
    /// Дата старше четырёх месяцев от сегодняшней ставится на следующий год
    /// </summary>
    [Fact]
    public void Parse_rolls_a_date_more_than_four_months_in_the_past_to_next_year()
    {
        const string csv = """
            ,Дата,время,РКИ-2-26
            пн,01.01,17.20-18.40,"Предмет, преподаватель"
            """;

        var slots = ScheduleParser.Parse(csv, "РКИ-2-26", Moscow, new DateOnly(2026, 6, 15));
        var lesson = Assert.Single(slots).Lesson;

        Assert.NotNull(lesson);
        Assert.Equal("up:rki-2-26:2027-01-01:1720", lesson.ExternalId);
        Assert.Equal(new DateOnly(2027, 1, 1), DateOnly.FromDateTime(lesson.Start.DateTime));
    }

    #endregion Facts
}
