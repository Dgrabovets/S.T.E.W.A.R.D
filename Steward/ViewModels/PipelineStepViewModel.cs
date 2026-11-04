namespace Steward.ViewModels;

/// <summary>
/// Один шаг в строке конвейера на карточке задания: «Найдено», «Готово», «Проверено» или «Сдано»
/// </summary>
/// <param name="Title">Название шага</param>
/// <param name="IsCurrent">Задание сейчас на этом шаге: шаг выделен</param>
/// <param name="IsPassed">Шаг уже пройден</param>
/// <param name="IsFirst">Первый шаг строки: перед ним нет разделителя</param>
public sealed record PipelineStepViewModel(string Title, bool IsCurrent, bool IsPassed, bool IsFirst)
{
    /// <summary>
    /// Имя шага для экранного диктора: название и состояние
    /// </summary>
    public string AccessibleName => IsCurrent ? $"{Title}: текущий шаг" : IsPassed ? $"{Title}: пройден" : $"{Title}: впереди";
}
