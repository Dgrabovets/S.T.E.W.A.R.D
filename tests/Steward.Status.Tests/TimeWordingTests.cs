using Steward.Status.Common;

namespace Steward.Status.Tests;

/// <summary>
/// Проверяет, как момент времени называется словами
/// </summary>
public class TimeWordingTests
{
    /// <summary>
    /// Сегодня, вчера и завтра называются словами, остальные дни датой
    /// </summary>
    [Theory]
    [InlineData(25, 9, "сегодня в 09:00")]
    [InlineData(24, 19, "вчера в 19:00")]
    [InlineData(26, 8, "завтра в 08:00")]
    [InlineData(20, 7, "20.10 в 07:00")]
    public void Moment_names_today_yesterday_and_tomorrow(int day, int hour, string expected)
    {
        var now = Samples.Winter(2026, 10, 25, 12);

        Assert.Equal(expected, TimeWording.Moment(Samples.Winter(2026, 10, day, hour), now));
    }

    /// <summary>
    /// Момент в другом поясе пересчитывается в пояс «сейчас»: 23:30 по UTC в Белграде уже следующее утро
    /// </summary>
    [Fact]
    public void Moment_converts_to_the_offset_of_now()
    {
        var now = Samples.Winter(2026, 10, 25, 12);
        var utc = new DateTimeOffset(2026, 10, 24, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal("сегодня в 00:30", TimeWording.Moment(utc, now));
    }
}