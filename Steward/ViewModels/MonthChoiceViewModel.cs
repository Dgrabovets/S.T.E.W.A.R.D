using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Один месяц в окошке выбора месяца календаря: название, выбран ли он сейчас и были ли в нём занятия
/// </summary>
/// <param name="Name">Название месяца</param>
/// <param name="IsSelected">Этот месяц показан в календаре</param>
/// <param name="HasLessons">В этом месяце есть занятия предмета</param>
/// <param name="Command">Показывает месяц в календаре</param>
public sealed record MonthChoiceViewModel(string Name, bool IsSelected, bool HasLessons, RelayCommand Command);
