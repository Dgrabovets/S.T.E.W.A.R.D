namespace Steward.Homework.Services;

/// <summary>
/// Шаг конвейера задания
/// </summary>
public enum PipelineStepEnum
{
    /// <summary>
    /// Задание найдено в расшифровке, работы ещё нет
    /// </summary>
    Found,

    /// <summary>
    /// Готовая работа лежит в папке
    /// </summary>
    Ready,

    /// <summary>
    /// Есть отчёт о проверке работы
    /// </summary>
    Checked,

    /// <summary>
    /// Владелец отметил задание сданным
    /// </summary>
    Submitted,
}

/// <summary>
/// Определяет шаг конвейера и порядок занятий. Состояние не хранится: шаг выводится из файлов и отметок владельца
/// </summary>
public static class PipelineStepper
{
    /// <summary>
    /// Определяет шаг задания по файлам и отметкам
    /// </summary>
    /// <param name="submitted">Владелец отметил задание сданным</param>
    /// <param name="hasDraft">Есть готовая работа</param>
    /// <param name="hasCheck">Есть отчёт о проверке</param>
    /// <returns>Шаг конвейера: сдано главнее проверки, проверка главнее готовой работы</returns>
    public static PipelineStepEnum Of(bool submitted, bool hasDraft, bool hasCheck)
    {
        if (submitted)
            return PipelineStepEnum.Submitted;

        if (hasCheck)
            return PipelineStepEnum.Checked;

        return hasDraft ? PipelineStepEnum.Ready : PipelineStepEnum.Found;
    }

    /// <summary>
    /// Определяет группу занятия в списке
    /// </summary>
    /// <param name="itemCount">Сколько у занятия заданий, кроме убранных в архив</param>
    /// <param name="pendingCount">Сколько из них ещё не сдано</param>
    /// <returns>0 — есть несданные задания, 1 — заданий нет, 2 — всё сдано</returns>
    public static int LessonGroup(int itemCount, int pendingCount)
    {
        if (itemCount <= 0)
            return 1;

        return pendingCount > 0 ? 0 : 2;
    }
}
