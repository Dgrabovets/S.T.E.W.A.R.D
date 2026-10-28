namespace Steward.ViewModels;

/// <summary>
/// Страницы главного окна: вкладки левой панели
/// </summary>
public enum AppPageEnum
{
    /// <summary>
    /// Состояние всех процессов
    /// </summary>
    Status,

    /// <summary>
    /// Идеи из рилсов, которые ждут решения
    /// </summary>
    Ideas,

    /// <summary>
    /// Задания и тесты из LMS
    /// </summary>
    Homework,

    /// <summary>
    /// Журнал запусков и сверок
    /// </summary>
    Journal,

    /// <summary>
    /// Почта: состояние процесса
    /// </summary>
    Mail,

    /// <summary>
    /// Расписание: состояние и настройки
    /// </summary>
    Schedule,

    /// <summary>
    /// Расшифровки занятий: предметы, отметки и форматы
    /// </summary>
    Transcripts,
}
