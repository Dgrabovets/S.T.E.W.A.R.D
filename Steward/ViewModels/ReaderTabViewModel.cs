using Steward.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Вкладка читалки расшифровки: название, выбрана ли она и команда выбора
/// </summary>
/// <param name="Tab">Какой раздел показывает вкладка</param>
/// <param name="Title">Название на вкладке</param>
/// <param name="IsActive">Вкладка выбрана</param>
/// <param name="SelectCommand">Показывает раздел этой вкладки</param>
public sealed record ReaderTabViewModel(ReaderTabEnum Tab, string Title, bool IsActive, RelayCommand SelectCommand)
{
    /// <summary>
    /// Имя вкладки для экранного диктора: название и состояние
    /// </summary>
    public string AccessibleName => IsActive ? $"{Title}: выбрана" : Title;
}
