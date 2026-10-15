namespace UltimateParser.Common.Helpers
{
    /// <summary>
    /// Класс-помощник для работы с объектами времени
    /// </summary>
    public static class TimeHelper
    {
        /// <summary>
        /// Находит часовой пояс по идентификатору IANA или Windows
        /// </summary>
        /// <param name="timeZoneId">
        /// Идентификатор, например <c>Europe/Moscow</c>
        /// </param>
        /// <returns>
        /// Найденный пояс.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Идентификатор неизвестен на этой машине
        /// </exception>
        public static TimeZoneInfo ResolveTimeZone(string timeZoneId)
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var timeZone))
                return timeZone;

            throw new InvalidOperationException($"Unknown time zone '{timeZoneId}'.");
        }
    }
}
