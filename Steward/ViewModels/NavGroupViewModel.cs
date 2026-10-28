namespace Steward.ViewModels;

/// <summary>
/// Группа вкладок в левой панели под общим заголовком, например «Учёба»
/// </summary>
/// <param name="Title">Заголовок группы</param>
/// <param name="Items">Вкладки группы сверху вниз</param>
public sealed record NavGroupViewModel(string Title, IReadOnlyList<NavItemViewModel> Items);
