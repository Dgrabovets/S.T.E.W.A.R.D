using System.Globalization;

namespace Steward.Status.Common;

/// <summary>
/// Говорит о моменте времени словами для строк состояния
/// </summary>
public static class TimeWording
{
    /// <summary>
    /// Описывает момент относительно «сейчас»: вчера, сегодня, завтра или датой
    /// </summary>
    /// <param name="time">Момент события</param>
    /// <param name="now">Текущий момент. По его смещению считаются границы дня</param>
    /// <returns>Фраза вида «сегодня в 09:00»</returns>
    public static string Moment(DateTimeOffset time, DateTimeOffset now)
    {
        var local = time.ToOffset(now.Offset);
        var clock = local.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (local.Date == now.Date)
            return $"сегодня в {clock}";

        if (local.Date == now.Date.AddDays(-1))
            return $"вчера в {clock}";

        if (local.Date == now.Date.AddDays(1))
            return $"завтра в {clock}";

        return $"{local:dd.MM} в {clock}";
    }
}