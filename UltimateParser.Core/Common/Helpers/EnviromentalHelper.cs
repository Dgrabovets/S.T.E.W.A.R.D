using UltimateParser.Common.Constants;
using UltimateParser.Common.Models;

namespace UltimateParser.Common.Helpers
{
    /// <summary>
    /// Класс для чтения настроек из переменных окружения процесса
    /// </summary>
    public static class EnviromentalHelper
    {
        /// <summary>
        /// Читает настройки из переменных окружения процесса
        /// </summary>
        /// <param name="runOnce">
        /// <see langword="true"/>, если в командной строке передан <c>--once</c>
        /// </param>
        /// <returns>
        /// Настройки из окружения: у всех полей есть значения по умолчанию, кроме токена
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Токен отсутствует, либо интервал не является положительным целым числом
        /// </exception>
        public static SyncOptions GetSyndOptionsFromEnvironment(bool runOnce)
        {
            return new SyncOptions
            {
                SingularityToken = TryGetEnviromentalVariable(EnviromentalParamsNames.SINGULARITY_TOKEN),
                SpreadsheetId = TryGetEnviromentalVariable(EnviromentalParamsNames.SPREADSHEET_ID),
                SheetGid = TryGetEnviromentalVariable(EnviromentalParamsNames.SHEET_GID),
                GroupColumn = TryGetEnviromentalVariable(EnviromentalParamsNames.GROUP_COLUMN),
                ProjectTitle = TryGetEnviromentalVariable(EnviromentalParamsNames.PROJECT_TITLE),
                TimeZoneId = TryGetEnviromentalVariable(EnviromentalParamsNames.TIME_ZONE),
                IntervalMinutes = ReadIntervalMinutes(),
                RunOnce = runOnce,
            };
        }

        /// <summary>
        /// Читает паузу в минутах из <c><see cref="EnviromentalParamsNames.SYNC_INTERVAL_MINUTES"/></c> или из <c><see cref="EnviromentalParamsNames.SYNC_INTERVAL_HOURS"/></c>
        /// </summary>
        /// <returns>
        /// Положительное число минут
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Интервал задан не положительным целым числом
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Интервал не указан
        /// </exception>
        private static int ReadIntervalMinutes()
        {
            var minutesText = Environment.GetEnvironmentVariable(EnviromentalParamsNames.SYNC_INTERVAL_MINUTES);
            if (!string.IsNullOrWhiteSpace(minutesText))
                return ParsePositive(minutesText, EnviromentalParamsNames.SYNC_INTERVAL_MINUTES);

            var hoursText = Environment.GetEnvironmentVariable(EnviromentalParamsNames.SYNC_INTERVAL_HOURS);
            if (!string.IsNullOrWhiteSpace(hoursText))
                return ParsePositive(hoursText, EnviromentalParamsNames.SYNC_INTERVAL_HOURS) * 60;

            throw new ArgumentNullException($"{nameof(EnviromentalParamsNames.SYNC_INTERVAL_MINUTES)} or {nameof(EnviromentalParamsNames.SYNC_INTERVAL_HOURS)} is required.");
        }

        /// <summary>
        /// Возвращает обязательную переменную окружения без крайних пробелов
        /// </summary>
        /// <param name="name">
        /// Имя переменной
        /// </param>
        /// <returns>
        /// Обрезанное значение
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Переменная отсутствует или пустая
        /// </exception>
        private static string TryGetEnviromentalVariable(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);

            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentNullException($"Environment variable {name} is empty, fill it in and rerun the application.");

            return value.Trim();
        }

        /// <summary>
        /// Разбирает положительное целое число из текста настройки
        /// </summary>
        /// <param name="text">
        /// Текст переменной окружения
        /// </param>
        /// <param name="name">
        /// Имя переменной для текста ошибки
        /// </param>
        /// <returns>
        /// Число больше нуля
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Текст не является положительным целым числом
        /// </exception>
        private static int ParsePositive(string text, string name)
        {
            if (!int.TryParse(text, out var value) || value < 1)
                throw new InvalidOperationException($"{name} must be a positive integer.");

            return value;
        }
    }
}
