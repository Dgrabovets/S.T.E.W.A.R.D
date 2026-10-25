namespace UltimateParser.Common.Constants
{
    /// <summary>
    /// Класс, содержащий наименование параметров в <c>.env</c>
    /// </summary>
    public static class EnviromentalParamsNames
    {
        /// <summary>
        /// Наименование параметра, содержащего токен из Singularity
        /// </summary>
        public const string SINGULARITY_TOKEN = "SINGULARITY_TOKEN";

        /// <summary>
        /// Наименование параметра, содержащего ID таблицы
        /// </summary>
        public const string SPREADSHEET_ID = "SPREADSHEET_ID";

        /// <summary>
        /// Наименование параметра, содержащего GID листа
        /// </summary>
        public const string SHEET_GID = "SHEET_GID";

        /// <summary>
        /// Наименование параметра, содержащего название столбца группы
        /// </summary>
        public const string GROUP_COLUMN = "GROUP_COLUMN";

        /// <summary>
        /// Наименование параметра, содержащего название проекта
        /// </summary>
        public const string PROJECT_TITLE = "PROJECT_TITLE";

        /// <summary>
        /// Наименование необязательного параметра, содержащего идентификатор родительского проекта
        /// </summary>
        public const string PROJECT_PARENT_ID = "PROJECT_PARENT_ID";

        /// <summary>
        /// Наименование необязательного параметра, включающего тег предмета на задачах занятий
        /// </summary>
        public const string SUBJECT_TAGS = "SUBJECT_TAGS";

        /// <summary>
        /// Наименование параметра, содержащего часовой пояс
        /// </summary>
        public const string TIME_ZONE = "TIME_ZONE";

        /// <summary>
        /// Наименование параметра, содержащего интервал синхронизации в минутах
        /// </summary>
        public const string SYNC_INTERVAL_MINUTES = "SYNC_INTERVAL_MINUTES";

        /// <summary>
        /// Наименование параметра, содержащего интервал синхронизации в часах
        /// </summary>
        public const string SYNC_INTERVAL_HOURS = "SYNC_INTERVAL_HOURS";
    }
}
